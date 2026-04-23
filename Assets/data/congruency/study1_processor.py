import pandas as pd
import matplotlib.pyplot as plt
import matplotlib.transforms as transforms
import matplotlib.cm as cm
import os
import numpy as np

# Load data
pids = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12]

all_trials = []
all_data = []

home_path = f"."

for pid in pids:
    file_path = f"{home_path}\\p_sheets\\p{pid}\\p{pid}_conditions.csv"
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
    """ plt.boxplot(
        grouped_visuals,
        positions=physical_levels,
        widths=0.1
    ) """

    # Overlay mean ± std
    plt.errorbar(
        physical_levels,
        means,
        yerr=stds,
        fmt='o',
        capsize=5
    )

    plt.xlabel("Physical Size (mm)")
    plt.ylabel("Relative Visual Size (%)")
    plt.xticks(np.arange(0, 130, 20))
    plt.yticks(np.arange(50, 210, 50))
    plt.title(f"P{pid}")

    #plt.show()

    # === SAVE FIGURE WITH SAME NAME AS CSV ===
    save_path = f"{home_path}\\output\\p{pid}.png"
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

# Compute mean and std per physicalSize
means = [
    np.mean(vals) if len(vals) > 0 else np.nan
    for vals in grouped_visuals_all
]

stds = [
    np.std(vals, ddof=1) if len(vals) > 1 else np.nan
    for vals in grouped_visuals_all
]

plt.figure()

""" plt.boxplot(
    grouped_visuals_all,
    positions=physical_levels_all,
    widths=0.1
) """

plt.errorbar(
        physical_levels,
        means,
        yerr=stds,
        fmt='o',
        capsize=5
    )

plt.xlabel("Physical Size (mm)")
plt.ylabel("Relative Visual Size (%)")
plt.xticks(np.arange(0, 130, 20))
plt.yticks(np.arange(50, 210, 50))
plt.title("All Participants Combined")

save_path = f"{home_path}\\output\\all_participants.png"
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

physical_levels = sorted(acceptance["physicalSize"].unique())
cmap = cm.gnuplot
colors = cmap(np.linspace(0, .85, len(physical_levels)))

plt.figure(figsize=(12, 6))

acceptance["trueVisualSize"] = acceptance["physicalSize"] * (acceptance["visualSize"] / 100)

for p, color in zip(physical_levels, colors):
    subset = acceptance[acceptance["physicalSize"] == p]

    plt.plot(
        subset["trueVisualSize"],
        subset["congruent"],
        marker="o",
        color=color,
        label=f"{p} mm"
    )

plt.axhline(0.70, linestyle="--", linewidth=1, color="gray")
plt.xticks(np.arange(0, 250, 20))
plt.yticks(np.arange(0, 1.1, .1))
plt.xlabel("True Visual Size (mm)")
plt.ylabel("P(Congruent Response)")
plt.title("Congruent Probability Curves")
plt.legend(title="Physical Size (mm)")

save_path = f"{home_path}\\output\\acceptance_curves.png"
plt.savefig(save_path, dpi=300, bbox_inches="tight")
plt.close()

# ===============================
# ACCEPTANCE CURVES PER PARTICIPANT
# ===============================

for pid in pids:
    file_path = f"{home_path}\\p_sheets\\p{pid}\\p{pid}_conditions.csv"
    df_p = pd.read_csv(file_path)

    df_p["physicalSize"] = pd.to_numeric(df_p["physicalSize"], errors="coerce")
    df_p["visualSize"] = pd.to_numeric(df_p["visualSize"], errors="coerce")

    acceptance_p = (
        df_p
        .groupby(["physicalSize", "visualSize"])["congruent"]
        .mean()
        .reset_index()
    )

    physical_levels = sorted(acceptance_p["physicalSize"].unique())
    colors = cmap(np.linspace(0, .85, len(physical_levels)))

    plt.figure(figsize=(12, 6))
    
    acceptance_p["trueVisualSize"] = acceptance["physicalSize"] * acceptance["visualSize"] / 100
    for p, color in zip(physical_levels, colors):
        subset = acceptance_p[acceptance_p["physicalSize"] == p]

        plt.plot(
            subset["trueVisualSize"],
            subset["congruent"],
            marker="o",
            color=color,
            label=f"{p} mm"
        )

    plt.axhline(0.70, linestyle="--", linewidth=1, color="gray")
    plt.xticks(np.arange(0, 250, 20))
    plt.yticks(np.arange(0, 1.1, .1))
    plt.xlabel("True Visual Size (mm)")
    plt.ylabel("P(Congruent Response)")
    plt.title(f"Participant {pid} – Acceptance Curves")
    plt.legend(title="Physical Size (mm)")

    save_path = f"{home_path}\\output\\p{pid}_acceptance_curves.png"
    plt.savefig(save_path, dpi=300, bbox_inches="tight")
    plt.close()
    
    
# ===============================
# Gradient Bars with Highlight
# ===============================

df_trials = pd.concat(all_trials, ignore_index=True)

acceptance = (
    df_trials
    .groupby(["physicalSize", "visualSize"])["congruent"]
    .mean()
    .reset_index()
)

acceptance["trueVisualSize"] = (
    acceptance["physicalSize"] * (acceptance["visualSize"] / 100)
)

heatmap_df = acceptance.pivot(
    index="trueVisualSize",
    columns="physicalSize",
    values="congruent"
)

# Sort axes (important for clean rendering)
heatmap_df = heatmap_df.sort_index()
heatmap_df = heatmap_df.sort_index(axis=1)

fig, ax = plt.subplots(figsize=(10, 6))

bar_width = 4  # skinny bar width in mm

for p in heatmap_df.columns:
    col = heatmap_df[p].dropna()
    y = col.index.values
    z = col.values.reshape(-1, 1)

    y_min_ext = y.min()
    y_max_ext = y.max()
    n = len(z)

    # Plot the gradient bar
    ax.imshow(
        z,
        extent=[
            p - bar_width / 2,
            p + bar_width / 2,
            y_min_ext,
            y_max_ext
        ],
        origin="lower",
        aspect="auto",
        cmap="magma",
        vmin=0,
        vmax=1,
        interpolation="bicubic"
    )

    # Draw tick line for probability >= 0.7
    mask = col.values >= 0.7
    if mask.any():
        idxs = np.where(mask)[0]
        y_coords = y_min_ext + (y_max_ext - y_min_ext) * (idxs / (n - 1))
        y_low = y_coords[0]
        y_high = y_coords[-1]

        # Draw small tick line at the left side of the bar
        tick_x = p + bar_width / 2 + 0.5  # slightly left of bar
        ax.plot([tick_x, tick_x], [y_low, y_high], color='gray', linewidth=2)

# Axes formatting
ax.set_xticks(np.arange(0, 121, 20))
ax.set_yticks(np.arange(0, 241, 20))

ax.set_xlim(0, 140)
ax.set_ylim(0, 260)

ax.set_xlabel("Physical Size (mm)")
ax.set_ylabel("True Visual Size (mm)")
ax.set_title("Acceptance Bands by Physical Size")

# Colorbar
sm = plt.cm.ScalarMappable(cmap="magma", norm=plt.Normalize(0, 1))
cbar = plt.colorbar(sm, ax=ax)
cbar.set_label("P(Congruent Response)")

ax.grid(axis='y', linestyle='--', alpha=0.7)
plt.tight_layout()

save_path = f"{home_path}\\output\\gradients.png"
plt.savefig(save_path, dpi=300, bbox_inches="tight")
plt.close()