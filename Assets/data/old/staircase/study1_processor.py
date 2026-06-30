import pandas as pd
import matplotlib.pyplot as plt
import matplotlib.transforms as transforms
import os

# Load data
pids = [1,2,3,4]

for pid in pids:
    file_names = ["oneHand_fixedVisual", "oneHand_fixedVolume"]
    for file_name in file_names:
        file_path = f"Assets\\data\\p_sheets\\p{pid}\\p{pid}_{file_name}.csv"
        df = pd.read_csv(file_path)

        # Choose which variable is plotted on the y-axis
        y_var = "visualSize" if "Volume" in file_path else "volumeSize"
        const_var = "visualSize" if y_var == "volumeSize" else "volumeSize"

        fig, ax = plt.subplots(figsize=(10, 5))

        for staircase, subdf in df.groupby("staircase"):
            subdf = subdf.sort_values("trial")

            line, = ax.plot(
                subdf["trial"],
                subdf[y_var],
                marker="o",
                label=f"Staircase {staircase}"
            )
            color = line.get_color()

            reversals = subdf[subdf["reversal"] == 1]
            ax.scatter(
                reversals["trial"],
                reversals[y_var],
                marker="x",
                s=80,
                linewidths=2,
                color=color
            )

            last_reversals = reversals.tail(8)

            if not last_reversals.empty:
                reversal_mean = last_reversals[y_var].mean()

                ax.axhline(
                    reversal_mean,
                    linestyle="dotted",
                    linewidth=2,
                    color=color,
                    label=f"{staircase} mean (last 8 rev.)"
                )

                trans = transforms.blended_transform_factory(
                    ax.transAxes, ax.transData
                )
                ax.text(
                    1.01,
                    reversal_mean,
                    f"{reversal_mean:.3f}",
                    color=color,
                    va="center",
                    ha="left",
                    transform=trans,
                    fontsize=10
                )

        ax.axhline(
            1,
            linestyle="solid",
            linewidth=2,
            color="black",
            label=f"{'Visual Size' if y_var == 'volumeSize' else 'Physical Size'} = +1cm"
        )

        ax.set_xlabel("Trial")
        ax.set_ylabel("Physical Size" if y_var == "volumeSize" else "Visual Size")
        ax.set_title(f"P{pid} Adaptive Physical Size" if y_var == "volumeSize" else f"P{pid} Adaptive Visual Size")
        ax.legend()
        ax.grid(True, alpha=0.3)

        plt.tight_layout()

        # === SAVE FIGURE WITH SAME NAME AS CSV ===
        save_path = f"Assets\\data\\output\\p{pid}_{file_name}.png"
        plt.savefig(save_path, dpi=300, bbox_inches="tight")

        #plt.show()
