import json
import numpy as np
from sklearn.cluster import HDBSCAN, KMeans
import charts

# Load data from JSON file
with open('State_SKLearn.json', 'r') as f:
    json_data = json.load(f)

# Check that the file format is correct
if isinstance(json_data, dict) and 'data' in json_data:
    entries = json_data['data']
else:
    raise ValueError("Invalid JSON format: missing 'data' object")

# Select top-level time intervals and values
timestamp_to_values_concatenated = {}
timestamp_to_values_aggregated = {}

for entry in entries:
    if isinstance(entry, dict) and 'starttime' in entry and 'values' in entry:
        start_time = entry['starttime']
        all_matrices = []
        for value_entry in entry['values']:
            if 'vehicles' in value_entry:
                matrix = np.array(value_entry['vehicles']).flatten()
                all_matrices.append(matrix)

        if all_matrices:
            timestamp_to_values_concatenated[start_time] = np.concatenate(all_matrices)  # combine data over a 30-minute interval and add to the list for clustering
            timestamp_to_values_aggregated[start_time] = np.mean(all_matrices, axis=0)   # take the average value per lane and add to the list for clustering

def Cluster(clusterer, values, file_name, scatter_chart_name, umap_chart_name, cluster_name):
    timestamps = list(values.keys())
    flattened_values = np.array(list(values.values()))
    # Perform clustering
    clusters = clusterer.fit_predict(flattened_values)
    np.savetxt(f"Clusters_{file_name}.csv", clusters, delimiter="\n")

    charts.drawCharts(json_data, timestamps, clusters, scatter_chart_name, umap_chart_name)

Cluster(
    HDBSCAN(metric='cosine', min_cluster_size=4),
    timestamp_to_values_aggregated,
    "HDBSCAN_AverageValues",
    "HDBSCAN Clustering (average values)",
    "UMAP Projection with HDBSCAN Clustering (average values)",
    "Silhouette Score for HDBSCAN (average values)"
)

Cluster(
    HDBSCAN(metric='cosine', min_cluster_size=4),
    timestamp_to_values_concatenated,
    "HDBSCAN_ConcatenatedValues",
    "HDBSCAN Clustering (concatenated values)",
    "UMAP Projection with HDBSCAN Clustering (concatenated values)",
    "Silhouette Score for HDBSCAN (concatenated values)"
)

Cluster(
    KMeans(n_clusters=5, random_state=None),
    timestamp_to_values_aggregated,
    "KMeans_5clusters_AverageValues",
    "KMeans Clustering (5 clusters) (average values)",
    "UMAP Projection with KMeans Clustering (5 clusters) (average values)",
    "Silhouette Score for KMeans (5 clusters) (average values)"
)

Cluster(
    KMeans(n_clusters=5, random_state=None),
    timestamp_to_values_concatenated,
    "KMeans_5clusters_ConcatenatedValues",
    "KMeans Clustering (5 clusters) (concatenated values)",
    "UMAP Projection with KMeans Clustering (5 clusters) (concatenated values)",
    "Silhouette Score for KMeans (5 clusters) (concatenated values)"
)

Cluster(
    KMeans(n_clusters=7, random_state=None),
    timestamp_to_values_aggregated,
    "KMeans_7clusters_AverageValues",
    "KMeans Clustering (7 clusters) (average values)",
    "UMAP Projection with KMeans Clustering (7 clusters) (average values)",
    "Silhouette Score for KMeans (7 clusters) (average values)"
)

Cluster(
    KMeans(n_clusters=7, random_state=None),
    timestamp_to_values_concatenated,
    "KMeans_7clusters_ConcatenatedValues",
    "KMeans Clustering (7 clusters) (concatenated values)",
    "UMAP Projection with KMeans Clustering (7 clusters) (concatenated values)",
    "Silhouette Score for KMeans (7 clusters) (concatenated values)"
)
