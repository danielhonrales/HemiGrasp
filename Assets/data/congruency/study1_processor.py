import pandas as pd
import matplotlib.pyplot as plt
import matplotlib.transforms as transforms
import matplotlib.patches as patches
import matplotlib.colors as mcolors
import matplotlib.cm as cm
import os
import numpy as np

# Load data
pids = [1, 2, 3, 5, 6, 7, 8, 9, 10, 11, 12]

all_trials = []
all_data = []

home_path = f"."

for pid in pids:
    file_path = f"{home_path}\\p_sheets\\p{pid}\\p{pid}_conditions.csv"
    df = pd.read_csv(file_path)
    print(df)

    df["physicalSize"] = pd.to_numeric(df["physicalSize"], errors="coerce")
    df["visualSize"] = pd.to_numeric(df["visualSize"], errors="coerce")

    all_trials.append(df.copy())
    df = df[df["congruent"] == 1]
    all_data.append(df)

    physical_levels = sorted(df["physicalSize"].unique())

    grouped_visuals = [
        df[df["physicalSize"] == p]["visualSize"].values
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

    plt.xlabel("Physical Size (mm)")
    plt.ylabel("Relative Visual Size (%)")
    plt.xticks(np.arange(0, 130, 20))
    plt.yticks(np.arange(50, 210, 50))
    plt.title(f"P{pid}")

    save_path = f"{home_path}\\output\\p{pid}.png"
    plt.savefig(save_path, dpi=300, bbox_inches="tight")


# ===============================
# COMBINED PLOT (ALL PARTICIPANTS)
# ===============================

df_all = pd.concat(all_data, ignore_index=True)

physical_levels_all = sorted(df_all["physicalSize"].unique())

grouped_visuals_all = [
    df_all[df_all["physicalSize"] == p]["visualSize"].values
    for p in physical_levels_all
]

means = [
    np.mean(vals) if len(vals) > 0 else np.nan
    for vals in grouped_visuals_all
]

stds = [
    np.std(vals, ddof=1) if len(vals) > 1 else np.nan
    for vals in grouped_visuals_all
]

plt.figure()

plt.errorbar(
    physical_levels_all,
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

    # FIXED: use acceptance_p instead of acceptance
    acceptance_p["trueVisualSize"] = acceptance_p["physicalSize"] * acceptance_p["visualSize"] / 100

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

heatmap_df = heatmap_df.sort_index()
heatmap_df = heatmap_df.sort_index(axis=1)

fig, ax = plt.subplots(figsize=(10, 6))

bar_width = 4

for p in heatmap_df.columns:
    col = heatmap_df[p].dropna()
    y = col.index.values
    z = col.values.reshape(-1, 1)

    y_min_ext = y.min()
    y_max_ext = y.max()

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

    plt.rcParams["hatch.linewidth"] = 2.0

    mask = col.values >= 0.7
    if mask.any():
        # FIXED: use actual y values directly
        y_low = y[mask].min()
        y_high = y[mask].max()

        hatch_patch = patches.Rectangle(
            (p - bar_width / 2, y_low),
            bar_width,
            y_high - y_low,
            linewidth=0,
            edgecolor='gray',
            facecolor='none',
            hatch='////',
            alpha=0.8
        )
        ax.add_patch(hatch_patch)

legend_patch = patches.Rectangle(
    (0, 0), 1, 1,
    linewidth=0,
    edgecolor='gray',
    facecolor='none',
    hatch='////',
    alpha=0.8,
    label='P ≥ 0.7'
)
ax.legend(handles=[legend_patch], loc='upper left')

ax.set_xticks(np.arange(0, 121, 20))
ax.set_yticks(np.arange(0, 241, 20))
ax.set_xlim(0, 140)
ax.set_ylim(0, 260)
ax.set_xlabel("Physical Size (mm)")
ax.set_ylabel("True Visual Size (mm)")
ax.set_title("Acceptance Bands by Physical Size")

sm = plt.cm.ScalarMappable(cmap="magma", norm=plt.Normalize(0, 1))
cbar = plt.colorbar(sm, ax=ax)
cbar.set_label("P(Congruent Response)")

ax.grid(axis='y', linestyle='--', alpha=0.7)
plt.tight_layout()

save_path = f"{home_path}\\output\\gradients.png"
plt.savefig(save_path, dpi=300, bbox_inches="tight")
plt.close()

# ===============================
# Gradient Bars with Highlight and Relative Subplot
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

heatmap_df = heatmap_df.sort_index()
heatmap_df = heatmap_df.sort_index(axis=1)

heatmap_df2 = acceptance.pivot(
    index="visualSize",
    columns="physicalSize",
    values="congruent"
)

heatmap_df2 = heatmap_df2.sort_index()
heatmap_df2 = heatmap_df2.sort_index(axis=1)

plt.rcParams["hatch.linewidth"] = 2.0

fig, (ax, ax2) = plt.subplots(
    2, 1,
    figsize=(10, 9),
    gridspec_kw={"height_ratios": [2, 1]}
)

def draw_gradient_bars(axis, df, bar_width=4):
    for p in df.columns:
        col = df[p].dropna()
        y = col.index.values
        z = col.values.reshape(-1, 1)

        y_min_ext = y.min()
        y_max_ext = y.max()

        axis.imshow(
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

        mask = col.values >= 0.7
        if mask.any():
            # FIXED: use actual y values directly
            y_low = y[mask].min()
            y_high = y[mask].max()

            hatch_patch = patches.Rectangle(
                (p - bar_width / 2, y_low),
                bar_width,
                y_high - y_low,
                linewidth=0,
                edgecolor='gray',
                facecolor='none',
                hatch='////',
                alpha=1.0
            )
            axis.add_patch(hatch_patch)

draw_gradient_bars(ax, heatmap_df, bar_width=6)
draw_gradient_bars(ax2, heatmap_df2, bar_width=6)

legend_patch = patches.Rectangle(
    (0, 0), 1, 1,
    linewidth=0,
    edgecolor='gray',
    facecolor='none',
    hatch='////',
    alpha=1.0,
    label='P ≥ 0.7'
)
ax.legend(handles=[legend_patch], loc='upper left')
ax2.legend(handles=[legend_patch], loc='upper left')

ax.set_xticks(np.arange(0, 121, 20))
ax.set_yticks(np.arange(0, 241, 20))
ax.set_xlim(0, 140)
ax.set_ylim(0, 260)
ax.set_xticklabels([])
ax.set_xlabel("")
ax.set_ylabel("True Visual Size (mm)")
ax.set_title("Acceptance Bands by Physical Size")
ax.grid(axis='y', linestyle='--', alpha=0.7)

ax2.set_xticks(np.arange(0, 121, 20))
ax2.set_xlim(0, 140)
ax2.set_xlabel("Physical Size (mm)")
ax2.set_yticks(np.arange(50, 201, 25))
ax2.set_ylim(50, 200)
ax2.set_ylabel("Relative Visual Size (%)")
ax2.set_title("")
ax2.grid(axis='y', linestyle='--', alpha=0.7)

plt.tight_layout(rect=[0, 0, 0.88, 1])
fig.subplots_adjust(hspace=0.08)

pos_top = ax.get_position()
pos_bot = ax2.get_position()

cbar_ax = fig.add_axes([
    0.90,
    pos_bot.y0,
    0.02,
    pos_top.y1 - pos_bot.y0
])

sm = plt.cm.ScalarMappable(cmap="magma", norm=plt.Normalize(0, 1))
cbar = fig.colorbar(sm, cax=cbar_ax)
cbar.set_label("P(Congruent Response)")

save_path = f"{home_path}\\output\\gradients_dual.png"
plt.savefig(save_path, dpi=300, bbox_inches="tight")
plt.close()

# ===============================
# Solid Black with Hatch and Relative Subplot
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

heatmap_df = heatmap_df.sort_index()
heatmap_df = heatmap_df.sort_index(axis=1)

heatmap_df2 = acceptance.pivot(
    index="visualSize",
    columns="physicalSize",
    values="congruent"
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

ax.set_xticks(np.arange(0, 121, 20))
ax.set_yticks(np.arange(0, 241, 20))
ax.set_xlim(0, 140)
ax.set_ylim(0, 260)
ax.set_xticklabels([])
ax.set_xlabel("")
ax.set_ylabel("True Visual Size (mm)")
ax.set_title("Acceptance Bands by Physical Size")
ax.grid(axis='y', linestyle='--', alpha=0.7)

ax2.set_xticks(np.arange(0, 121, 20))
ax2.set_xlim(0, 140)
ax2.set_xlabel("Physical Size (mm)")
ax2.set_yticks(np.arange(50, 201, 25))
ax2.set_ylim(50, 200)
ax2.set_ylabel("Relative Visual Size (%)")
ax2.set_title("")
ax2.grid(axis='y', linestyle='--', alpha=0.7)

plt.tight_layout()
fig.subplots_adjust(hspace=0.08)

save_path = f"{home_path}\\output\\solid_dual.png"
plt.savefig(save_path, dpi=300, bbox_inches="tight")
plt.close()

# ===============================
# Pastel Rainbow Colors by Physical Size
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
heatmap_df = heatmap_df.sort_index()
heatmap_df = heatmap_df.sort_index(axis=1)

heatmap_df2 = acceptance.pivot(
    index="visualSize",
    columns="physicalSize",
    values="congruent"
)
heatmap_df2 = heatmap_df2.sort_index()
heatmap_df2 = heatmap_df2.sort_index(axis=1)

all_physical_sizes = sorted(set(list(heatmap_df.columns) + list(heatmap_df2.columns)))
n_sizes = len(all_physical_sizes)

pastel_colors = [
    mcolors.hsv_to_rgb([i / n_sizes, 0.55, 0.85])
    for i in range(n_sizes)
]
color_map = dict(zip(all_physical_sizes, pastel_colors))

plt.rcParams["hatch.linewidth"] = 2.0

fig, (ax, ax2) = plt.subplots(
    2, 1,
    figsize=(10, 9),
    gridspec_kw={"height_ratios": [2, 1]}
)

def draw_bars(axis, df, bar_width=4):
    for p in df.columns:
        col = df[p].dropna()
        y = col.index.values
        y_min_ext = y.min()
        y_max_ext = y.max()

        base_color = color_map[p]

        hatch_patch = patches.Rectangle(
            (p - bar_width / 2, y_min_ext),
            bar_width,
            y_max_ext - y_min_ext,
            linewidth=0.5,
            edgecolor=base_color,
            facecolor='none',
            hatch='////',
            alpha=1.0
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
                facecolor=base_color,
                alpha=1.0
            )
            axis.add_patch(solid_patch)

draw_bars(ax, heatmap_df, bar_width=6)
draw_bars(ax2, heatmap_df2, bar_width=6)

ax2.axhline(y=100, color='0.3', linewidth=1.2, linestyle='-', zorder=3)

size_legend_handles = [
    patches.Patch(facecolor=color_map[p], edgecolor='gray', label=f'{p} mm')
    for p in all_physical_sizes
]

hatch_patch_legend = patches.Rectangle(
    (0, 0), 1, 1,
    linewidth=0.5,
    edgecolor='gray',
    facecolor='none',
    hatch='////',
    label='Full range tested'
)
solid_patch_legend = patches.Rectangle(
    (0, 0), 1, 1,
    linewidth=0,
    edgecolor='none',
    facecolor='dimgray',
    label='P ≥ 0.7'
)

ax.legend(
    handles=[hatch_patch_legend, solid_patch_legend] + size_legend_handles,
    loc='upper left',
    ncol=1
)

ax.set_xticks(np.arange(0, 121, 20))
ax.set_yticks(np.arange(0, 241, 20))
ax.set_xlim(0, 140)
ax.set_ylim(0, 260)
ax.set_xticklabels([])
ax.set_xlabel("")
ax.set_ylabel("True Visual Size (mm)")
ax.set_title("Acceptance Bands by Physical Size")
ax.grid(axis='y', linestyle='--', alpha=0.7)

ax2.set_xticks(np.arange(0, 121, 20))
ax2.set_xlim(0, 140)
ax2.set_xlabel("Physical Size (mm)")
ax2.set_yticks(np.arange(50, 201, 25))
ax2.set_ylim(50, 200)
ax2.set_ylabel("Relative Visual Size (%)")
ax2.set_title("")
ax2.grid(axis='y', linestyle='--', alpha=0.7)

plt.tight_layout()
fig.subplots_adjust(hspace=0.08)

save_path = f"{home_path}\\output\\rainbow_dual.png"
plt.savefig(save_path, dpi=300, bbox_inches="tight")
plt.close()