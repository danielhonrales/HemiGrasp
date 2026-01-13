import pandas as pd
import matplotlib.pyplot as plt
import matplotlib.transforms as transforms
import os
import numpy as np

# Load data
pids = [1,2,3,4]

all_trials = []
all_data = []

for pid in pids:
    file_path = f"Assets\\data\\congruency\\p_sheets\\p{pid}\\p{pid}_conditions.csv"
    df = pd.read_csv(file_path)
    print(df)

    # Ensure numeric (safety)
    df["physicalSize"] = pd.to_numeric(df["physicalSize"], errors="coerce")
    df["visualSize"] = pd.to_numeric(df["visualSize"], errors="coerce")

    all_trials.append(df.copy())
    # Filter congruent trials
    df = df[df["congruent"] == 1]
    all_data.append(df)

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
    plt.title(f"P{pid}")

    #plt.show()

    # === SAVE FIGURE WITH SAME NAME AS CSV ===
    save_path = f"Assets\\data\\congruency\\output\\p{pid}.png"
    plt.savefig(save_path, dpi=300, bbox_inches="tight")

    #plt.show()


# ===============================
# COMBINED PLOT (ALL PARTICIPANTS)
# ===============================

df_all = pd.concat(all_data, ignore_index=True)

# Get sorted physical size levels
physical_levels_all = sorted(df_all["physicalSize"].unique())

# Group visualSize by physicalSize
grouped_visuals_all = [
    df_all[df_all["physicalSize"] == p]["visualSize"].values
    for p in physical_levels_all
]

plt.figure()

plt.boxplot(
    grouped_visuals_all,
    positions=physical_levels_all,
    widths=0.1
)

plt.xlabel("Physical Size")
plt.ylabel("Visual Size")
plt.title("All Participants Combined")

save_path = "Assets\\data\\congruency\\output\\all_participants.png"
plt.savefig(save_path, dpi=300, bbox_inches="tight")
plt.close()


# ===============================
# Acceptability Curve
# ===============================

df_trials = pd.concat(all_trials, ignore_index=True)

acceptance = (
    df_trials
    .groupby(["physicalSize", "visualSize"])["congruent"]
    .mean()
    .reset_index()
)

plt.figure(figsize=(8, 6))

physical_levels = sorted(acceptance["physicalSize"].unique())

for p in physical_levels:
    subset = acceptance[acceptance["physicalSize"] == p]

    plt.plot(
        subset["visualSize"],
        subset["congruent"],
        marker="o",
        label=f"physicalSize = {p}"
    )

plt.axhline(0.70, linestyle="--", linewidth=1)
plt.yticks(np.arange(0, 1.01, 0.1))
plt.xlabel("Visual Size")
plt.ylabel("P(Congruent Response)")
plt.title("Congruent Probability Curves")
plt.ylim(0, 1.05)
plt.legend(title="Physical Size")

save_path = "Assets\\data\\congruency\\output\\acceptance_curves.png"
plt.savefig(save_path, dpi=300, bbox_inches="tight")
plt.close()

# ===============================
# ACCEPTANCE CURVES PER PARTICIPANT
# ===============================

for pid in pids:
    file_path = f"Assets\\data\\congruency\\p_sheets\\p{pid}\\p{pid}_conditions.csv"
    df_p = pd.read_csv(file_path)

    # Ensure numeric
    df_p["physicalSize"] = pd.to_numeric(df_p["physicalSize"], errors="coerce")
    df_p["visualSize"] = pd.to_numeric(df_p["visualSize"], errors="coerce")

    # Compute acceptance probabilities
    acceptance_p = (
        df_p
        .groupby(["physicalSize", "visualSize"])["congruent"]
        .mean()
        .reset_index()
    )

    plt.figure(figsize=(8, 6))

    physical_levels = sorted(acceptance_p["physicalSize"].unique())

    for p in physical_levels:
        subset = acceptance_p[acceptance_p["physicalSize"] == p]

        plt.plot(
            subset["visualSize"],
            subset["congruent"],
            marker="o",
            label=f"physicalSize = {p}"
        )

    plt.axhline(0.70, linestyle="--", linewidth=1)
    plt.yticks(np.arange(0, 1.01, 0.1))
    plt.xlabel("Visual Size")
    plt.ylabel("P(Congruent Response)")
    plt.title(f"Participant {pid} – Acceptance Curves")
    plt.ylim(0, 1.05)
    plt.legend(title="Physical Size")

    save_path = f"Assets\\data\\congruency\\output\\p{pid}_acceptance_curves.png"
    plt.savefig(save_path, dpi=300, bbox_inches="tight")
    plt.close()