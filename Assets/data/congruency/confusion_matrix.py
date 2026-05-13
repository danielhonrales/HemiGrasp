import pandas as pd
import matplotlib.pyplot as plt
import matplotlib.colors as mcolors
import os
import numpy as np

# ===============================
# CONFIG
# ===============================

pids = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12]
home_path = "."

# Fixed relative visual size levels (%), largest at top
VISUAL_SIZE_LEVELS = [200, 175, 150, 125, 100, 87.5, 75, 67.5, 50]

# ===============================
# LOAD ALL TRIALS
# ===============================

all_trials = []

for pid in pids:
    file_path = f"{home_path}\\p_sheets\\p{pid}\\p{pid}_conditions.csv"
    df = pd.read_csv(file_path)
    df["physicalSize"] = pd.to_numeric(df["physicalSize"], errors="coerce")
    df["visualSize"] = pd.to_numeric(df["visualSize"], errors="coerce")
    df["pid"] = pid
    all_trials.append(df)

df_all = pd.concat(all_trials, ignore_index=True)


def plot_confusion_matrix(df, title, save_path):
    """
    Plots a confusion matrix heatmap:
      - X axis: Physical Size (mm)
      - Y axis: Relative Visual Size (%)
      - Cell color: P(Congruent Response)
    """

    # Aggregate: mean congruent response per (physicalSize, visualSize) cell
    agg = (
        df.groupby(["physicalSize", "visualSize"])["congruent"]
        .mean()
        .reset_index()
    )

    # Pivot to matrix form
    matrix = agg.pivot(index="visualSize", columns="physicalSize", values="congruent")

    # Reindex rows to fixed levels (largest at top), filling missing with NaN
    matrix = matrix.reindex(VISUAL_SIZE_LEVELS)

    # Sort columns smallest to largest
    matrix = matrix.sort_index(axis=1)

    x_labels = matrix.columns.values    # physical sizes
    y_labels = matrix.index.values      # relative visual sizes (%)

    fig, ax = plt.subplots(figsize=(8, 9))

    cmap = plt.cm.Blues
    norm = mcolors.Normalize(vmin=0, vmax=1)

    im = ax.imshow(
        matrix.values,
        cmap=cmap,
        norm=norm,
        aspect="auto",
        interpolation="nearest"
    )

    # Axis ticks
    ax.set_xticks(np.arange(len(x_labels)))
    ax.set_yticks(np.arange(len(y_labels)))
    ax.set_xticklabels([f"{int(v)}" for v in x_labels], fontsize=8)
    ax.set_yticklabels([f"{v:.1f}%" for v in y_labels], fontsize=7)

    # Annotate cells with the probability value
    for row_i, y_val in enumerate(y_labels):
        for col_i, x_val in enumerate(x_labels):
            val = matrix.loc[y_val, x_val]
            if not np.isnan(val):
                text_color = "black" if 0.2 < val < 0.8 else "white"
                ax.text(
                    col_i, row_i,
                    f"{val:.2f}",
                    ha="center", va="center",
                    fontsize=6, color=text_color
                )

    # Labels and title
    ax.set_xlabel("Physical Size (mm)", fontsize=12)
    ax.set_ylabel("Relative Visual Size (%)", fontsize=12)
    ax.set_title(title, fontsize=14)

    cbar = plt.colorbar(im, ax=ax, fraction=0.046, pad=0.04)
    cbar.set_label("P(Congruent Response)", fontsize=11)
    cbar.set_ticks([0, 0.25, 0.5, 0.75, 1.0])

    plt.tight_layout()
    os.makedirs(os.path.dirname(save_path) if os.path.dirname(save_path) else ".", exist_ok=True)
    plt.savefig(save_path, dpi=300, bbox_inches="tight")
    plt.close()
    print(f"Saved: {save_path}")


# ===============================
# COMBINED — ALL PARTICIPANTS
# ===============================

plot_confusion_matrix(
    df_all,
    title="Confusion Matrix – All Participants\nP(Congruent Response) by Physical Size × Relative Visual Size",
    save_path=f"{home_path}\\output\\confusion_matrix_all.png"
)

# ===============================
# PER PARTICIPANT
# ===============================

for pid in pids:
    df_p = df_all[df_all["pid"] == pid]
    plot_confusion_matrix(
        df_p,
        title=f"Confusion Matrix – Participant {pid}\nP(Congruent Response) by Physical Size × Relative Visual Size",
        save_path=f"{home_path}\\output\\p{pid}_confusion_matrix.png"
    )

print("Done.")