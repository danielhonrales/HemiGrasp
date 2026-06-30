import os
import glob
import pandas as pd
import numpy as np
import matplotlib.pyplot as plt

pids = [3,4]

data_dir = os.path.join(os.path.dirname(__file__), "p_sheets")
csv_files = glob.glob(os.path.join(data_dir, "**", "*_conditions.csv"), recursive=True)

all_df = pd.concat([pd.read_csv(f) for f in csv_files], ignore_index=True)
all_df = all_df[all_df["pid"].isin(pids)]

scenarios = sorted(all_df["scenario"].unique())
visual_sizes = sorted(all_df["visualSize"].unique())

base_results = os.path.join(os.path.dirname(__file__), "results")

def generate_figures(df, results_dir, label):
    os.makedirs(results_dir, exist_ok=True)

    fig, axes = plt.subplots(len(scenarios), 1, figsize=(10, 5 * len(scenarios)), sharey=True, sharex=True)
    if len(scenarios) == 1:
        axes = [axes]

    for row, scenario in enumerate(scenarios):
        ax = axes[row]
        sdf = df[df["scenario"] == scenario]
        grouped = sdf.groupby("visualSize")["congruency"].mean()

        colors = ['#416cc1' if v >= 0.7 else '#b0b0b0' for v in grouped.values]
        ax.bar(grouped.index, grouped.values, width=15, color=colors)
        ax.axhline(y=0.7, color='red', linestyle='--', alpha=0.7, label='P = 0.7')

        ax.set_xticks(visual_sizes)
        ax.set_ylabel("P(Congruency)")
        ax.set_title(scenario)
        ax.set_ylim(0, 1)
        ax.legend()
        ax.grid(axis='y', linestyle='--', alpha=0.3)

        if row == len(scenarios) - 1:
            ax.set_xlabel("Visual Size (mm)")

    fig.suptitle(f"P(Congruency) by Visual Size — Expand ({label})")
    plt.tight_layout()
    fig.savefig(os.path.join(results_dir, "congruency.png"), dpi=300, bbox_inches="tight")
    plt.close()

    for metric, ylabel, filename in [("time", "Average Time (s)", "time.png"),
                                      ("distance", "Average Distance", "distance.png")]:
        fig, axes = plt.subplots(len(scenarios), 1, figsize=(10, 5 * len(scenarios)), sharey=True, sharex=True)
        if len(scenarios) == 1:
            axes = [axes]

        for row, scenario in enumerate(scenarios):
            ax = axes[row]
            sdf = df[df["scenario"] == scenario]
            grouped = sdf.groupby("visualSize")[metric].mean()

            ax.bar(grouped.index, grouped.values, width=15, color='#416cc1')
            ax.set_xticks(visual_sizes)
            ax.set_ylabel(ylabel)
            ax.set_title(scenario)
            ax.grid(axis='y', linestyle='--', alpha=0.3)

            if row == len(scenarios) - 1:
                ax.set_xlabel("Visual Size (mm)")

        fig.suptitle(f"{ylabel} by Visual Size — Expand ({label})")
        plt.tight_layout()
        fig.savefig(os.path.join(results_dir, filename), dpi=300, bbox_inches="tight")
        plt.close()

if min(pids) != max(pids):
    group_dir = os.path.join(base_results, f"p{min(pids)}-{max(pids)}")
else:
    group_dir = os.path.join(base_results, f"p{min(pids)}")
generate_figures(all_df, group_dir, f"p{min(pids)}-{max(pids)}" if min(pids) != max(pids) else f"p{min(pids)}")

for pid in pids:
    pid_dir = os.path.join(base_results, f"p{pid}")
    generate_figures(all_df[all_df["pid"] == pid], pid_dir, f"p{pid}")
