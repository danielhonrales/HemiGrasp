import os
import glob
import pandas as pd
import numpy as np
import matplotlib.pyplot as plt
import matplotlib.patches as patches

pids = [1,2]

data_dir = os.path.join(os.path.dirname(__file__), "p_sheets")
csv_files = glob.glob(os.path.join(data_dir, "**", "*_conditions.csv"), recursive=True)

df = pd.concat([pd.read_csv(f) for f in csv_files], ignore_index=True)
df = df[df["pid"].isin(pids)]

speeds = sorted(df["physical_speed"].unique(), reverse=True)
scenarios = sorted(df["scenario"].unique())

if min(pids) != max(pids):
    results_dir = os.path.join(os.path.dirname(__file__), "results", f"p{min(pids)}-{max(pids)}")
else:
    results_dir = os.path.join(os.path.dirname(__file__), "results", f"p{min(pids)}")
os.makedirs(results_dir, exist_ok=True)

# --- Congruency curves (rows = scenarios, cols = speeds) ---
fig, axes = plt.subplots(len(scenarios), len(speeds),
                         figsize=(4 * len(speeds), 4 * len(scenarios)), sharey=True, sharex=True)

for row, scenario in enumerate(scenarios):
    sdf = df[df["scenario"] == scenario]
    for col, speed in enumerate(speeds):
        ax = axes[row, col]
        subset = sdf[sdf["physical_speed"] == speed]
        grouped = subset.groupby("visual_end_size")["congruency"].mean()
        ax.plot(grouped.index, grouped.values, marker="o")
        if row == 0:
            ax.set_title(f"Speed = {speed}")
        if row == len(scenarios) - 1:
            ax.set_xlabel("Visual End Size")
            ax.set_xticks(grouped.index)
        if col == 0:
            ax.set_ylabel(f"{scenario}\nP(Congruency)")
        ax.set_ylim(0, 1)

fig.suptitle("P(Congruency) by Visual End Size per Physical Speed — Shrink")
plt.tight_layout()
fig.savefig(os.path.join(results_dir, "congruency.png"), dpi=300, bbox_inches="tight")
plt.close()

# --- Range bars (rows = scenarios) ---
plt.rcParams["hatch.linewidth"] = 2.0
fig2, axes2 = plt.subplots(len(scenarios), 1, figsize=(10, 6 * len(scenarios)), sharey=True, sharex=True)
bar_width = 6

for row, scenario in enumerate(scenarios):
    ax2 = axes2[row]
    sdf = df[df["scenario"] == scenario]
    grouped_all = sdf.groupby(["physical_speed", "visual_end_size"])["congruency"].mean()

    for speed in speeds:
        series = grouped_all[speed].sort_index()
        y = series.index.values
        y_min, y_max = y.min(), y.max()

        hatch_patch = patches.Rectangle(
            (speed - bar_width / 2, y_min),
            bar_width,
            y_max - y_min,
            linewidth=0.5,
            edgecolor='#416cc1',
            facecolor='none',
            hatch='////',
            alpha=0.8
        )
        ax2.add_patch(hatch_patch)

        mask = series.values >= 0.7
        if mask.any():
            y_low = y[mask].min()
            y_high = y[mask].max()
            solid_patch = patches.Rectangle(
                (speed - bar_width / 2, y_low),
                bar_width,
                y_high - y_low,
                linewidth=0,
                edgecolor='none',
                facecolor='#416cc1',
                alpha=1.0
            )
            ax2.add_patch(solid_patch)

    if row == 0:
        hatch_legend = patches.Rectangle(
            (0, 0), 1, 1,
            linewidth=0.5, edgecolor='#416cc1', facecolor='none',
            hatch='////', alpha=0.8, label='Full range tested'
        )
        solid_legend = patches.Rectangle(
            (0, 0), 1, 1,
            linewidth=0, edgecolor='none', facecolor='#416cc1',
            label='P ≥ 0.7'
        )
        ax2.legend(handles=[hatch_legend, solid_legend], loc='upper left')

    ax2.set_xticks(speeds)
    ax2.set_xlim(max(speeds) + 10, min(speeds) - 10)
    ax2.set_ylim(30, 65)
    ax2.set_yticks(np.arange(35, 65, 5))
    ax2.set_ylabel(f"{scenario}\nVisual End Size (%)")
    ax2.set_title(f"Congruency Bands — {scenario}")
    ax2.grid(axis='y', linestyle='--', alpha=0.7)
    if row == len(scenarios) - 1:
        ax2.set_xlabel("Physical Speed (mm/s)")

fig2.suptitle("Congruency Bands by Physical Speed — Shrink")
fig2.tight_layout()
fig2.savefig(os.path.join(results_dir, "congruency_range.png"), dpi=300, bbox_inches="tight")
plt.close()
