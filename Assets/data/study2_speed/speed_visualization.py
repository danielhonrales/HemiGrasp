import pandas as pd
import matplotlib.pyplot as plt
import matplotlib.transforms as transforms
import matplotlib.patches as patches
import matplotlib.colors as mcolors
import matplotlib.cm as cm
import os
import numpy as np

# Load data
pids = [3]

all_trials = []
all_data = []

home_path = f"."

for pid in pids:
    file_path = f"{home_path}\\p_sheets\\p{pid}\\p{pid}_conditions.csv"
    df = pd.read_csv(file_path)
    print(df)

    df["physical"] = pd.to_numeric(df["physical"], errors="coerce")
    df["visual"] = pd.to_numeric(df["visual"], errors="coerce")

    all_trials.append(df.copy())
    df = df[df["binary"] == 1]
    all_data.append(df)

    physical_levels = sorted(df["physical"].unique())

    grouped_visuals = [
        df[df["physical"] == p]["visual"].values
        for p in physical_levels
    ]

    means = [
        np.mean(vals) if len(vals) > 0 else np.nan
        for vals in grouped_visuals
    ]

    stds = [
        np.std(vals, ddof=1) if len(vals) > 1 else np.nan
        for vals in grouped_visuals
    ]

    plt.figure()

    plt.errorbar(
        physical_levels,
        means,
        yerr=stds,
        fmt='o',
        capsize=5
    )

    plt.xlabel("Physical Speed (mm/s)")
    plt.ylabel("Relative Visual Speed (%)")
    plt.xticks(np.arange(0, 50, 10))
    plt.yticks(np.arange(50, 210, 50))
    plt.title(f"P{pid}")

    save_path = f"{home_path}\\output\\p{pid}.png"
    plt.savefig(save_path, dpi=300, bbox_inches="tight")

# All participants

# Grow

df_trials = pd.concat(all_trials, ignore_index=True)
df_trials = df_trials[df_trials["direction"] == "grow"]

acceptance = (
    df_trials
    .groupby(["physical", "visual"])["binary"]
    .mean()
    .reset_index()
)

acceptance["trueVisual"] = (
    acceptance["physical"] * (acceptance["visual"] / 100)
)

heatmap_df = acceptance.pivot(
    index="trueVisual",
    columns="physical",
    values="binary"
)

heatmap_df = heatmap_df.sort_index()
heatmap_df = heatmap_df.sort_index(axis=1)

heatmap_df2 = acceptance.pivot(
    index="visual",
    columns="physical",
    values="binary"
)

heatmap_df2 = heatmap_df2.sort_index()
heatmap_df2 = heatmap_df2.sort_index(axis=1)

plt.rcParams["hatch.linewidth"] = 2.0

fig, (ax, ax2) = plt.subplots(
    2, 1,
    figsize=(10, 9),
    gridspec_kw={"height_ratios": [2, 1]}
)

def draw_bars_top(axis, df, bar_width=4):
    for p in df.columns:
        col = df[p].dropna()
        y = col.index.values
        y_min_ext = y.min()
        y_max_ext = y.max()

        hatch_patch = patches.Rectangle(
            (p - bar_width / 2, y_min_ext),
            bar_width,
            y_max_ext - y_min_ext,
            linewidth=0.5,
            edgecolor='#416cc1',
            facecolor='none',
            hatch='////',
            alpha=0.8
        )
        axis.add_patch(hatch_patch)

        mask = col.values >= 0.7
        if mask.any():
            # FIXED: use actual y values directly
            y_low = y[mask].min()
            y_high = y[mask].max()

            solid_patch = patches.Rectangle(
                (p - bar_width / 2, y_low),
                bar_width,
                y_high - y_low,
                linewidth=0,
                edgecolor='none',
                facecolor='#416cc1',
                alpha=1.0
            )
            axis.add_patch(solid_patch)


def draw_bars_bottom(axis, df, bar_width=4):
    for p in df.columns:
        col = df[p].dropna()
        y = col.index.values
        y_min_ext = y.min()
        y_max_ext = y.max()

        hatch_patch = patches.Rectangle(
            (p - bar_width / 2, y_min_ext),
            bar_width,
            y_max_ext - y_min_ext,
            linewidth=0.5,
            edgecolor='#416cc1',
            facecolor='none',
            hatch='////',
            alpha=0.8
        )
        axis.add_patch(hatch_patch)

        mask = col.values >= 0.7
        if mask.any():
            # FIXED: use actual y values directly
            y_low = y[mask].min()
            y_high = y[mask].max()

            solid_patch = patches.Rectangle(
                (p - bar_width / 2, y_low),
                bar_width,
                y_high - y_low,
                linewidth=0,
                edgecolor='none',
                facecolor='#416cc1',
                alpha=1.0
            )
            axis.add_patch(solid_patch)

draw_bars_top(ax, heatmap_df, bar_width=6)
draw_bars_bottom(ax2, heatmap_df2, bar_width=6)

ax2.axhline(y=100, color='0.3', linewidth=1.2, linestyle='-', zorder=3)

hatch_patch_legend = patches.Rectangle(
    (0, 0), 1, 1,
    linewidth=0.5,
    edgecolor='#416cc1',
    facecolor='none',
    hatch='////',
    alpha=0.8,
    label='Full range tested'
)
solid_patch_legend = patches.Rectangle(
    (0, 0), 1, 1,
    linewidth=0,
    edgecolor='none',
    facecolor='#416cc1',
    label='P ≥ 0.7'
)
ax.legend(handles=[hatch_patch_legend, solid_patch_legend], loc='upper left')

ax.set_xticks(np.arange(0, 41, 10))
ax.set_yticks(np.arange(0, 161, 20))
ax.set_xlim(0, 50)
ax.set_ylim(0, 160)
ax.set_xticklabels([])
ax.set_xlabel("")
ax.set_ylabel("True Visual Speed (mm/s)")
ax.set_title("Acceptance Bands by Physical Speed - Grow")
ax.grid(axis='y', linestyle='--', alpha=0.7)

ax2.set_xticks(np.arange(0, 41, 10))
ax2.set_xlim(0, 50)
ax2.set_xlabel("Physical Speed (mm/s)")
ax2.set_yticks(np.arange(25, 401, 50))
ax2.set_ylim(25, 400)
ax2.set_ylabel("Relative Visual Speed (%)")
ax2.set_title("")
ax2.grid(axis='y', linestyle='--', alpha=0.7)

plt.tight_layout()
fig.subplots_adjust(hspace=0.08)

save_path = f"{home_path}\\output\\all_grow.png"
plt.savefig(save_path, dpi=300, bbox_inches="tight")
plt.close()

# Shrink

df_trials = pd.concat(all_trials, ignore_index=True)
df_trials = df_trials[df_trials["direction"] == "shrink"]

acceptance = (
    df_trials
    .groupby(["physical", "visual"])["binary"]
    .mean()
    .reset_index()
)

acceptance["trueVisual"] = (
    acceptance["physical"] * (acceptance["visual"] / 100)
)

heatmap_df = acceptance.pivot(
    index="trueVisual",
    columns="physical",
    values="binary"
)

heatmap_df = heatmap_df.sort_index()
heatmap_df = heatmap_df.sort_index(axis=1)

heatmap_df2 = acceptance.pivot(
    index="visual",
    columns="physical",
    values="binary"
)

heatmap_df2 = heatmap_df2.sort_index()
heatmap_df2 = heatmap_df2.sort_index(axis=1)

plt.rcParams["hatch.linewidth"] = 2.0

fig, (ax, ax2) = plt.subplots(
    2, 1,
    figsize=(10, 9),
    gridspec_kw={"height_ratios": [2, 1]}
)

def draw_bars_top(axis, df, bar_width=4):
    for p in df.columns:
        col = df[p].dropna()
        y = col.index.values
        y_min_ext = y.min()
        y_max_ext = y.max()

        hatch_patch = patches.Rectangle(
            (p - bar_width / 2, y_min_ext),
            bar_width,
            y_max_ext - y_min_ext,
            linewidth=0.5,
            edgecolor='#416cc1',
            facecolor='none',
            hatch='////',
            alpha=0.8
        )
        axis.add_patch(hatch_patch)

        mask = col.values >= 0.7
        if mask.any():
            # FIXED: use actual y values directly
            y_low = y[mask].min()
            y_high = y[mask].max()

            solid_patch = patches.Rectangle(
                (p - bar_width / 2, y_low),
                bar_width,
                y_high - y_low,
                linewidth=0,
                edgecolor='none',
                facecolor='#416cc1',
                alpha=1.0
            )
            axis.add_patch(solid_patch)


def draw_bars_bottom(axis, df, bar_width=4):
    for p in df.columns:
        col = df[p].dropna()
        y = col.index.values
        y_min_ext = y.min()
        y_max_ext = y.max()

        hatch_patch = patches.Rectangle(
            (p - bar_width / 2, y_min_ext),
            bar_width,
            y_max_ext - y_min_ext,
            linewidth=0.5,
            edgecolor='#416cc1',
            facecolor='none',
            hatch='////',
            alpha=0.8
        )
        axis.add_patch(hatch_patch)

        mask = col.values >= 0.7
        if mask.any():
            # FIXED: use actual y values directly
            y_low = y[mask].min()
            y_high = y[mask].max()

            solid_patch = patches.Rectangle(
                (p - bar_width / 2, y_low),
                bar_width,
                y_high - y_low,
                linewidth=0,
                edgecolor='none',
                facecolor='#416cc1',
                alpha=1.0
            )
            axis.add_patch(solid_patch)

draw_bars_top(ax, heatmap_df, bar_width=6)
draw_bars_bottom(ax2, heatmap_df2, bar_width=6)

ax2.axhline(y=100, color='0.3', linewidth=1.2, linestyle='-', zorder=3)

hatch_patch_legend = patches.Rectangle(
    (0, 0), 1, 1,
    linewidth=0.5,
    edgecolor='#416cc1',
    facecolor='none',
    hatch='////',
    alpha=0.8,
    label='Full range tested'
)
solid_patch_legend = patches.Rectangle(
    (0, 0), 1, 1,
    linewidth=0,
    edgecolor='none',
    facecolor='#416cc1',
    label='P ≥ 0.7'
)
ax.legend(handles=[hatch_patch_legend, solid_patch_legend], loc='upper left')

ax.set_xticks(np.arange(0, 41, 10))
ax.set_yticks(np.arange(0, 161, 20))
ax.set_xlim(0, 50)
ax.set_ylim(0, 160)
ax.set_xticklabels([])
ax.set_xlabel("")
ax.set_ylabel("True Visual Speed (mm/s)")
ax.set_title("Acceptance Bands by Physical Speed - Shrink")
ax.grid(axis='y', linestyle='--', alpha=0.7)

ax2.set_xticks(np.arange(0, 41, 10))
ax2.set_xlim(0, 50)
ax2.set_xlabel("Physical Speed (mm/s)")
ax2.set_yticks(np.arange(25, 401, 50))
ax2.set_ylim(25, 400)
ax2.set_ylabel("Relative Visual Speed (%)")
ax2.set_title("")
ax2.grid(axis='y', linestyle='--', alpha=0.7)

plt.tight_layout()
fig.subplots_adjust(hspace=0.08)

save_path = f"{home_path}\\output\\all_shrink.png"
plt.savefig(save_path, dpi=300, bbox_inches="tight")
plt.close()