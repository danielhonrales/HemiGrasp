"""
confusion_matrix.py
Generate confusion matrices from study2_shape/p_sheets condition data.
Edit the parameters below, then run: python confusion_matrix.py
"""

import os

import matplotlib.pyplot as plt
import pandas as pd
import seaborn as sns

# ── Parameters ────────────────────────────────────────────────────────────────
# Which participants to include.
# Use a range string "1-12", a list "1,2,3,4", or mixed "1-4,6,8-10".
PARTICIPANTS = "2"

# Generate one matrix per participant in addition to the aggregate.
PER_PARTICIPANT = False
# ──────────────────────────────────────────────────────────────────────────────

SHAPES = ["small", "medium", "large", "convex", "concave", "slope"]
BASE_DIR = os.path.dirname(os.path.abspath(__file__))
P_SHEETS_DIR = os.path.join(BASE_DIR, "p_sheets")
RESULTS_DIR = os.path.join(BASE_DIR, "results")


def parse_participants(spec: str) -> list:
    participants = []
    for part in spec.split(","):
        part = part.strip()
        if "-" in part:
            start, end = part.split("-", 1)
            participants.extend(range(int(start), int(end) + 1))
        else:
            participants.append(int(part))
    return sorted(set(participants))


def load_data(participants: list) -> pd.DataFrame:
    dfs = []
    for pid in participants:
        csv_path = os.path.join(P_SHEETS_DIR, f"p{pid}", f"p{pid}_conditions.csv")
        if not os.path.exists(csv_path):
            print(f"  Warning: {csv_path} not found, skipping p{pid}.")
            continue
        df = pd.read_csv(csv_path)
        dfs.append(df)
    if not dfs:
        raise FileNotFoundError("No participant CSV files found for the given selection.")
    df = pd.concat(dfs, ignore_index=True)
    if df["congruency"].isna().all():
        print("  Warning: 'congruency' column is empty for all loaded trials.")
    return df


def build_matrix(df: pd.DataFrame) -> pd.DataFrame:
    """Pivot table: rows = visual (y-axis), columns = physical (x-axis), values = mean congruency."""
    cm = df.pivot_table(
        index="visual",
        columns="physical",
        values="congruency",
        aggfunc="mean",
    )
    cm = cm.reindex(index=SHAPES, columns=SHAPES)
    return cm


def plot_cm(cm: pd.DataFrame, title: str, output_path: str):
    fig, ax = plt.subplots(figsize=(8, 7))
    sns.heatmap(
        cm,
        annot=True,
        fmt=".2f",
        cmap="Blues",
        linewidths=0.5,
        linecolor="lightgray",
        square=True,
        ax=ax,
        vmin=0,
        vmax=3.0,
        cbar_kws={"label": "Mean Congruency", "shrink": 0.8},
    )
    ax.set_title(title, fontsize=13, pad=10)
    ax.set_xlabel("Physical Shape", fontsize=11, labelpad=6)
    ax.set_ylabel("Visual Shape", fontsize=11, labelpad=6)
    plt.xticks(rotation=45, ha="right", fontsize=10)
    plt.yticks(rotation=0, fontsize=10)
    plt.tight_layout()
    fig.savefig(output_path, dpi=150, bbox_inches="tight")
    plt.close(fig)
    print(f"  Saved figure: {output_path}")


def run_aggregate(df: pd.DataFrame, participants: list, spec_clean: str):
    output_dir = os.path.join(RESULTS_DIR, f"p{spec_clean}")
    os.makedirs(output_dir, exist_ok=True)

    cm = build_matrix(df)

    n = len(participants)
    p_label = f"p{participants[0]}-p{participants[-1]}" if n > 1 else f"p{participants[0]}"
    title = f"Mean Congruency — {p_label}  (n={n} participant{'s' if n > 1 else ''})"

    plot_cm(cm, title, os.path.join(output_dir, "confusion_matrix.png"))
    cm.to_csv(os.path.join(output_dir, "confusion_matrix.csv"))
    print(f"  Saved CSV:    {os.path.join(output_dir, 'confusion_matrix.csv')}")


def run_per_participant(df: pd.DataFrame, participants: list, spec_clean: str):
    output_dir = os.path.join(RESULTS_DIR, f"p{spec_clean}")
    os.makedirs(output_dir, exist_ok=True)

    for pid in participants:
        sub = df[df["pid"] == pid]
        if sub.empty:
            print(f"  No data for p{pid}, skipping.")
            continue
        cm = build_matrix(sub)
        title = f"Mean Congruency — p{pid}"
        plot_cm(cm, title, os.path.join(output_dir, f"p{pid}_confusion_matrix.png"))

    run_aggregate(df, participants, spec_clean)


def main():
    participants = parse_participants(PARTICIPANTS)
    spec_clean = PARTICIPANTS.replace(" ", "")

    print(f"Participants selected: {participants}")
    print(f"Loading data...")
    df = load_data(participants)
    print(f"Total trials loaded: {len(df)}")
    print(f"Output directory: results/p{spec_clean}/\n")

    if PER_PARTICIPANT:
        run_per_participant(df, participants, spec_clean)
    else:
        run_aggregate(df, participants, spec_clean)

    print("\nDone.")


if __name__ == "__main__":
    main()
