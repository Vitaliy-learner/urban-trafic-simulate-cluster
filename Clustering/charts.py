import numpy as np
import pandas as pd
import matplotlib.pyplot as plt
import umap

# Method for grouping data into 1-minute intervals and calculating average values per lane
def compare_numeric_arrays(x):
    arrays = [arr for arr in x if isinstance(arr, np.ndarray) and arr.size > 0]
    if arrays:
        stacked = np.stack(arrays)
        return np.mean(stacked, axis=0)  # Use the average value for each element by index
    return np.zeros((10, 7))  # Fill with zeros if no data is available

def drawCharts(json_data, timestamps, clusters, scatter_chart_name, umap_chart_name):
    # Create clusters_data with starttime-endtime range
    clusters_data = [{'starttime': pd.to_datetime(x), 'endtime': pd.to_datetime(x) + pd.Timedelta(minutes=30), 'cluster': y} for x, y in zip(timestamps, clusters)]

    # Create data_frame_resampled
    unique_data = {}
    for entry in json_data['data']:
        for value in entry['values']:
            timestamp = pd.to_datetime(value['timestamp'])
            if timestamp not in unique_data:  # Add only if timestamp is not already in the list
                unique_data[timestamp] = np.array(value['vehicles'])

    # Convert to a list of format [{'timestamp': ..., 'vehicles': ...}, ...]
    result = [{'timestamp': ts, 'values': veh} for ts, veh in unique_data.items()]

    # Create DataFrame
    data_frame = pd.DataFrame(result, columns=['timestamp', 'values'])
    data_frame.set_index('timestamp', inplace=True)

    # Group data into 1-minute intervals
    data_frame_resampled = data_frame.resample('1min').agg(compare_numeric_arrays).dropna()

    # Add a cluster column with initial value -1 (noise)
    data_frame_resampled['cluster'] = -1

    # Convert clusters_data to DataFrame
    clusters_df = pd.DataFrame(clusters_data)

    # Convert starttime and endtime columns to datetime
    clusters_df['starttime'] = pd.to_datetime(clusters_df['starttime'])
    clusters_df['endtime'] = pd.to_datetime(clusters_df['endtime'])

    # Assign clusters if timestamp is within the range starttime - endtime
    for index, row in data_frame_resampled.iterrows():
        timestamp = row.name
        matching_cluster = clusters_df[(clusters_df['starttime'] <= timestamp) & (clusters_df['endtime'] >= timestamp)]

        if not matching_cluster.empty:
            cluster = matching_cluster['cluster'].mode()[0]
            data_frame_resampled.at[index, 'cluster'] = cluster

    filtered_data_frame = data_frame_resampled[data_frame_resampled['cluster'] != -1]

    # Plot charts
    drawScatterPlot(filtered_data_frame, scatter_chart_name)
    drawUMAP(filtered_data_frame, umap_chart_name)

def drawScatterPlot(data_frame, chart_name):
    unique_clusters = np.unique(data_frame['cluster'])

    plt.figure(figsize=(12, 6))
    for cluster in unique_clusters:
        cluster_data = data_frame[data_frame['cluster'] == cluster]

        # Filter out rows where all array elements are equal to 0
        filtered_cluster_data = cluster_data[[not np.all(x == 0) for x in cluster_data['values']]]

        if not filtered_cluster_data.empty:
            plt.scatter(
                filtered_cluster_data.index,
                [np.mean(x) for x in filtered_cluster_data['values']],
                label=f'Cluster {cluster + 1}'
            )

    plt.xlabel('Time')
    plt.ylabel('Average value')
    plt.title(chart_name)
    plt.legend()
    plt.xticks(rotation=45, ha='right')
    plt.gca().xaxis.set_major_formatter(plt.matplotlib.dates.DateFormatter('%H:%M'))
    plt.show()

# ------------------- UMAP -------------------

def drawUMAP(data_frame, chart_name):
    # Identify groups of continuous segments with the same cluster
    data_frame['cluster_change'] = (data_frame['cluster'] != data_frame['cluster'].shift()).cumsum()

    # Group by continuous segments (using cluster_change) and select the middle element
    middle_indices = data_frame.groupby('cluster_change').apply(lambda x: x.iloc[len(x) // 2])

    # Convert data to vectorized format
    X_similarity = np.array([arr.flatten() for arr in middle_indices['values'].values])

    # Dimensionality reduction using UMAP
    umap_reducer = umap.UMAP(n_components=2, n_neighbors=2, min_dist=0.5, spread=2, metric='cosine')
    embedding_umap = umap_reducer.fit_transform(X_similarity)

    # Add UMAP results to DataFrame
    middle_indices['UMAP_1'] = embedding_umap[:, 0]
    middle_indices['UMAP_2'] = embedding_umap[:, 1]

    # Visualize UMAP with clusters
    plt.figure(figsize=(10, 6))
    scatter = plt.scatter(middle_indices['UMAP_1'], middle_indices['UMAP_2'], c=middle_indices['cluster'], s=100)
    plt.xlabel('UMAP Dimension 1')
    plt.ylabel('UMAP Dimension 2')
    plt.title(chart_name)
    plt.colorbar(scatter, label='Cluster')
    plt.show()
