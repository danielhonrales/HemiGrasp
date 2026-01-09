import pandas as pd
import matplotlib.pyplot as plt
import matplotlib.transforms as transforms
import os
import numpy as np

# Load data
pids = [1]

for pid in pids:
    file_path = f"Assets\\data\\congruency\\p_sheets\\p{pid}\\p{pid}_conditions.csv"
    df = pd.read_csv(file_path)
    print(df)

    # Values to exclude
    exclude_vals = [0.25, 0.75, 1.25, 1.75]

    # Ensure numeric (safety)
    df["physicalSize"] = pd.to_numeric(df["physicalSize"], errors="coerce")
    df["visualSize"] = pd.to_numeric(df["visualSize"], errors="coerce")

    # Filter out excluded sizes
    df = df[
        ~df["physicalSize"].isin(exclude_vals) &
        ~df["visualSize"].isin(exclude_vals)
    ]

    # Filter congruent trials
    df = df[df["congruent"] == 1]

    # Get sorted physical size levels
    physical_levels = sorted(df["physicalSize"].unique())

    # Group visualSize by physicalSize
    grouped_visuals = [
        df[df["physicalSize"] == p]["visualSize"].values
        for p in physical_levels
    ]

    # Compute mean and std per physicalSize
    means = [
        np.mean(vals) if len(vals) > 0 else np.nan
        for vals in grouped_visuals
    ]

    stds = [
        np.std(vals, ddof=1) if len(vals) > 1 else np.nan
        for vals in grouped_visuals
    ]

    # -------------------------------
    # Plot
    # -------------------------------
    plt.figure()

    # Box-and-whisker plot
    plt.boxplot(
        grouped_visuals,
        positions=physical_levels,
        widths=0.1
    )

    # Overlay mean ± std
    """ plt.errorbar(
        physical_levels,
        means,
        yerr=stds,
        fmt='o',
        capsize=5
    ) """

    plt.xlabel("Physical Size")
    plt.ylabel("Visual Size")
    plt.title("Visual Size Distributions for Congruent Trials")

    plt.show()

    # === SAVE FIGURE WITH SAME NAME AS CSV ===
    save_path = f"Assets\\data\\output\\p{pid}.png"
    plt.savefig(save_path, dpi=300, bbox_inches="tight")

    #plt.show()
