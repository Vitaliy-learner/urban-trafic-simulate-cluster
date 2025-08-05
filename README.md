# Adaptive Cascade Clustering for Urban Traffic Flow Analysis

This repository implements the research from the article:

> **Establishing patterns of urban traffic flows using an adaptive cascade clustering approach**  
> _Vitaliy Pavlyshyn, Eduard Manziuk, Oleksander Barmak, Pavlo Radiuk, Iurii Krak_  
> *Sustainability, 2025 (under review)*

---

## Overview

This repository contains the implementation necessary to reproduce the research presented in the article  
**"Establishing patterns of urban traffic flows using an adaptive cascade clustering approach"**.

The main goal of the study is to improve the identification of traffic modes in urban environments by using an adaptive combination of HDBSCAN and KMeans algorithms with automatic parameter tuning and strategy selection based on clustering quality criteria.

### Core Features

- **Time Window Representation:** The traffic network is divided into fixed-length time windows. Each window is described by a feature vector that includes:
  - Average traffic flow across intersections
  - Standard deviation and variability
  - Rate of change in flow
  - Temporal dependencies (autocorrelation)

- **Cascade Clustering Pipeline:**
  - **HDBSCAN** is used to detect underlying structure and outliers with automatic parameter selection.
  - **KMeans** is applied using HDBSCAN centroids for improved compactness and boundary clarity.
  - A **weighted voting system** selects the best result based on cluster validity metrics (e.g., V-measure, ARI, silhouette score).

- **Adaptive Strategy Selection:** The system dynamically chooses between:
  - **HDBSCAN-first** (for noisy or heterogeneous data)
  - **KMeans-first** (for well-structured, low-noise data)
  - **Hybrid mode** (when data characteristics are intermediate)

- **Validation:** The approach is validated through experiments on a SUMO simulation model of Khmelnytskyi, Ukraine. The system successfully detects key transport patterns (e.g., peak hours, low-activity periods) and preserves time coherence in cluster assignments.

This framework lays the foundation for real-time, adaptive, and environmentally-aware urban traffic control systems.

---

## Repository Structure

```
.
├── SumoLauncher/           # C# project: launches SUMO simulations
├── DataTransformation/     # C# project: processes and transforms traffic data
├── Clustering/             # Python project: clustering with HDBSCAN and KMeans
```

---

## 1. `SumoLauncher/` — SUMO Simulation Runner (C#)

Launches a SUMO simulation and generates traffic state data for further processing.

### Setup

Before running, copy the following into `SumoLauncher/bin/Debug/`:

- `sumo.exe` — SUMO executable
- Route file (`*.rou.xml`)
- Network and additional map files (`*.net.xml`, `*.add.xml`, etc.)

### Output

```
State.json
```

Contains timestamped vehicle movement data from the simulation.

### Usage

```bash
cd SumoLauncher/bin/Debug
SumoLauncher.exe
```

---

## 2. `DataTransformation/` — Time Window Feature Extraction (C#)

Transforms raw simulation data into machine-learning-ready format via time window segmentation.

### Input

```
State.json
```

### Output

```
State_SKLearn.json
```

This structured file includes features such as flow rates, statistical variability, and autocorrelation, suitable for clustering.

---

## 3. `Clustering/` — Traffic Mode Identification (Python)

Applies unsupervised clustering using a cascade of:

- **HDBSCAN** — density-based clustering with noise handling
- **KMeans** — centroidal refinement initialized from HDBSCAN
- **Weighted Voting** — automatic selection of the best result
- **Adaptive Strategy Switching** — based on data profile

### Input

```
State_SKLearn.json
```

### Usage

```bash
cd Clustering
python cluster.py --input ../DataTransformation/State_SKLearn.json
```

Produces visualizations and evaluation metrics such as:
- V-measure
- Adjusted Rand Index (ARI)
- Silhouette Score
- Time coherence

---

## Requirements

**For Python (`Clustering/`)**:
- Python 3.8+
- `numpy`, `pandas`, `scikit-learn`, `hdbscan`, `matplotlib`, `umap-learn`

Install dependencies:

```bash
pip install -r Clustering/requirements.txt
```

**For C# (`SumoLauncher/`, `DataTransformation/`)**:
- .NET Framework 4.7.2+ or .NET Core 3.1+
- Visual Studio or `dotnet` CLI