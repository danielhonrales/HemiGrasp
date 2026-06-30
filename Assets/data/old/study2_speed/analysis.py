"""
Psychophysics analysis: physical speed x visual speed multiplier matching study
- Within-subject, two blocks per participant (grow / shrink direction)
- Binary response: congruent (1=yes match, 0=no match)
- Factors: physical speed (10/20/30/40), visual multiplier (25/50/75/100/200/300/400 %),
  direction (grow/shrink)
- GLMM with random effects per participant
"""

import os
import glob
import numpy as np
import pandas as pd
import matplotlib.pyplot as plt
import matplotlib.cm as cm
from scipy.optimize import curve_fit
from scipy import stats
import warnings
warnings.filterwarnings("ignore")

# ── Optional: statsmodels for GLMM ──────────────────────────────────────────
try:
    import statsmodels.formula.api as smf
    HAS_STATSMODELS = True
except ImportError:
    HAS_STATSMODELS = False
    print("statsmodels not found. Install with: pip install statsmodels")


# =============================================================================
# 1. LOAD DATA
# =============================================================================

def load_all_participants(base_dir="p_sheets"):
    """
    Load CSVs from /p_sheets/p{N}/p{N}_conditions.csv
    Columns: pid, trial, physical, visual, direction, congruency, binary
    """
    pattern = os.path.join(base_dir, "p*", "p*_conditions.csv")
    files = sorted(glob.glob(pattern))

    if not files:
        raise FileNotFoundError(
            f"No files found matching {pattern}\n"
            "Check that base_dir points to your p_sheets folder."
        )

    dfs = []
    for f in files:
        df = pd.read_csv(f)
        dfs.append(df)
        print(f"  Loaded {f}  ({len(df)} trials)")

    data = pd.concat(dfs, ignore_index=True)
    print(f"\nTotal trials loaded: {len(data)}")
    return data


def prepare_data(data):
    """
    Clean and add derived columns.
    visual is a multiplier where 100 = 100% = veridical speed match.
    """
    data = data.copy()

    data["pid"]      = data["pid"].astype(str)
    data["physical"] = data["physical"].astype(float)
    data["visual"]   = data["visual"].astype(float)
    data["direction"] = data["direction"].astype(str)

    # Exclude participants who haven't completed the task yet
    # (binary response not yet recorded)
    nan_counts = data.groupby("pid")["binary"].apply(lambda s: s.isna().sum())
    excluded_pids = nan_counts[nan_counts > 0.5 * data.groupby("pid").size().max()].index
    if len(excluded_pids) > 0:
        print(f"Excluding participants with mostly missing 'binary' data: {list(excluded_pids)}")
        data = data[~data["pid"].isin(excluded_pids)]

    n_missing = data["binary"].isna().sum()
    if n_missing > 0:
        print(f"Dropping {n_missing} row(s) with missing 'binary' values")
        data = data.dropna(subset=["binary"])

    # Use 'congruent' as the response column name throughout (1 = match, 0 = no match)
    data["congruent"] = data["binary"].astype(int)

    # Log ratio: 0 = perfect veridical match, positive = visual faster than physical
    data["log_ratio"] = np.log(data["visual"] / 100.0)

    # Centered versions for better model convergence
    data["visual_c"]   = (data["visual"]  - data["visual"].mean())  / data["visual"].std()
    data["physical_c"] = (data["physical"] - data["physical"].mean()) / data["physical"].std()

    # Physical speed as ordered category
    data["physical_cat"] = pd.Categorical(
        data["physical"],
        categories=sorted(data["physical"].unique()),
        ordered=True
    )

    return data


# =============================================================================
# 2. DESCRIPTIVE STATS
# =============================================================================

def descriptive_stats(data):
    print("\n" + "="*60)
    print("DESCRIPTIVE STATISTICS")
    print("="*60)
    print(f"Participants  : {data['pid'].nunique()} ({sorted(data['pid'].unique(), key=int)})")
    print(f"Physical speeds: {sorted(data['physical'].unique())}")
    print(f"Visual multipliers: {sorted(data['visual'].unique())}")
    print(f"Directions    : {sorted(data['direction'].unique())}")
    print(f"Trials per participant: {data.groupby('pid').size().to_dict()}")
    print(f"Overall 'yes' rate: {data['congruent'].mean():.3f}")

    for d in sorted(data["direction"].unique()):
        sub = data[data["direction"] == d]
        print(f"  '{d}' rate: {sub['congruent'].mean():.3f}  (n={len(sub)})")

    cell_rates = (
        data.groupby(["direction", "physical", "visual"])["congruent"]
        .agg(["mean", "count"])
        .rename(columns={"mean": "p_yes", "count": "n"})
        .reset_index()
    )
    print("\nResponse rates (first 10 rows):")
    print(cell_rates.head(10).to_string(index=False))
    return cell_rates


# =============================================================================
# 3. PSYCHOMETRIC CURVE FITTING (per physical speed, per direction, group-level)
# =============================================================================

def bell_curve(x, pse, width, peak):
    """
    Bell-shaped (Gaussian) psychometric function for matching tasks.
    pse   = point of subjective equality (peak location)
    width = spread (sigma) of the acceptance region
    peak  = maximum P(yes) at the PSE
    Returns P(yes).
    """
    return peak * np.exp(-((x - pse) ** 2) / (2 * width ** 2))


def fit_psychometric_curves(data):
    """
    Aggregate across participants per (physical, visual) cell,
    then fit a bell curve per physical speed.
    Returns a dict: physical -> {pse, width, lower_70, upper_70, ...}
    """
    cell = (
        data.groupby(["physical", "visual"])["congruent"]
        .mean()
        .reset_index()
    )

    results = {}
    phys_speeds = sorted(cell["physical"].unique())

    for ps in phys_speeds:
        sub = cell[cell["physical"] == ps].sort_values("visual")
        x = sub["visual"].values
        y = sub["congruent"].values

        try:
            p0 = [100.0, 75.0, y.max()]
            bounds = ([x.min(), 1.0, 0.0], [x.max(), x.max() - x.min(), 1.0])
            popt, pcov = curve_fit(bell_curve, x, y, p0=p0, bounds=bounds, maxfev=5000)
            pse, width, peak = popt
            perr = np.sqrt(np.diag(pcov))

            x_fine = np.linspace(x.min(), x.max(), 10000)
            y_fine = bell_curve(x_fine, pse, width, peak)

            def threshold_crossing(thresh):
                above = x_fine[y_fine >= thresh]
                return (above[0] if len(above) > 0 else np.nan,
                        above[-1] if len(above) > 0 else np.nan)

            l50, u50 = threshold_crossing(0.50)
            l70, u70 = threshold_crossing(0.70)
            l75, u75 = threshold_crossing(0.75)

            results[ps] = {
                "pse": pse, "width": width, "peak": peak,
                "pse_se": perr[0], "width_se": perr[1], "peak_se": perr[2],
                "lower_50": l50, "upper_50": u50,
                "lower_70": l70, "upper_70": u70,
                "lower_75": l75, "upper_75": u75,
                "width_70": u70 - l70,
                "width_75": u75 - l75,
                "bias": pse - 100.0,          # positive = overestimate visual speed
                "x": x, "y": y,
                "x_fine": x_fine, "y_fine": y_fine,
            }
            print(f"  phys={ps:5.0f}  PSE={pse:6.1f}  width={width:5.1f}  peak={peak:4.2f}"
                  f"  bias={pse-100:+.1f}  70%-zone=[{l70:.1f}, {u70:.1f}]")

        except RuntimeError as e:
            print(f"  phys={ps}: curve fit failed - {e}")
            results[ps] = None

    return results


def summarise_pse(curve_results):
    rows = []
    for ps, r in curve_results.items():
        if r is None:
            continue
        rows.append({
            "physical": ps,
            "PSE": r["pse"],
            "PSE_SE": r["pse_se"],
            "bias": r["bias"],
            "width_70": r["width_70"],
            "width_75": r["width_75"],
            "lower_70": r["lower_70"],
            "upper_70": r["upper_70"],
        })
    df = pd.DataFrame(rows)
    print("\nPSE Summary (veridical = 100):")
    print(df.to_string(index=False))
    return df


# =============================================================================
# 4. PSE LINEAR REGRESSION (does PSE scale with physical speed?)
# =============================================================================

def pse_linear_regression(pse_df):
    """
    Tests whether PSE increases linearly with physical speed.
    Uses the group-level PSE values from curve fitting.
    """
    print("\n" + "="*60)
    print("PSE LINEAR REGRESSION (PSE ~ physical)")
    print("="*60)

    x = pse_df["physical"].values
    y = pse_df["PSE"].values

    slope, intercept, r, p, se = stats.linregress(x, y)

    print(f"  Slope     : {slope:.4f}  (SE={se:.4f})")
    print(f"  Intercept : {intercept:.3f}")
    print(f"  R^2        : {r**2:.4f}")
    print(f"  r         : {r:.4f}")
    print(f"  p-value   : {p:.4f}{'*' if p < 0.05 else ''}")

    if p < 0.05:
        print(f"  -> Significant: PSE changes by {slope:.2f} units per unit of physical speed.")
    else:
        print(f"  -> Not significant: no reliable linear trend of PSE with physical speed.")

    p_one_tailed = p / 2 if slope > 0 else 1 - p / 2
    print(f"  One-tailed p (slope > 0): {p_one_tailed:.4f}{'*' if p_one_tailed < 0.05 else ''}")

    return {"slope": slope, "intercept": intercept, "r": r, "r2": r**2,
            "p": p, "se": se, "p_one_tailed": p_one_tailed}


def pse_regression_individual(ind_pse_df):
    """
    Same regression but using individual PSEs — one data point per
    participant x physical speed.
    """
    if ind_pse_df.empty:
        return

    print("\n" + "="*60)
    print("PSE LINEAR REGRESSION - individual level")
    print("="*60)

    x = ind_pse_df["physical"].values
    y = ind_pse_df["PSE"].values
    slope, intercept, r, p, se = stats.linregress(x, y)
    print(f"  OLS (ignoring participant structure)")
    print(f"  Slope={slope:.4f}  R^2={r**2:.4f}  p={p:.4f}{'*' if p < 0.05 else ''}")

    print(f"\n  Per-participant slopes:")
    slopes = []
    for pid in sorted(ind_pse_df["pid"].unique()):
        sub = ind_pse_df[ind_pse_df["pid"] == pid]
        if len(sub) < 3:
            continue
        s, ic, r_, p_, se_ = stats.linregress(sub["physical"], sub["PSE"])
        slopes.append(s)
        print(f"    pid={pid}  slope={s:.3f}  p={p_:.3f}{'*' if p_ < 0.05 else ''}")

    if len(slopes) < 2:
        print("\n  Not enough participants with converged curves for a group slope test.")
        return

    slopes = np.array(slopes)
    t, p_group = stats.ttest_1samp(slopes, popmean=0)
    print(f"\n  One-sample t-test: are individual slopes different from 0?")
    print(f"  Mean slope={slopes.mean():.4f} +/- {slopes.std():.4f}")
    print(f"  t={t:.3f}  p={p_group:.4f}{'*' if p_group < 0.05 else ''}")


# =============================================================================
# 5. INDIVIDUAL PSEs (per participant x physical speed)
# =============================================================================

def fit_individual_pses(data):
    """
    Fit per-participant bell curves.
    With only 5 reps, curves will be noisy — use with caution.
    """
    rows = []
    for pid in sorted(data["pid"].unique(), key=int):
        for ps in sorted(data["physical"].unique()):
            sub = (
                data[(data["pid"] == pid) & (data["physical"] == ps)]
                .groupby("visual")["congruent"]
                .mean()
                .reset_index()
                .sort_values("visual")
            )
            if len(sub) < 3:
                continue
            x, y = sub["visual"].values, sub["congruent"].values
            try:
                popt, _ = curve_fit(bell_curve, x, y,
                                    p0=[100.0, 75.0, y.max()],
                                    bounds=([x.min(), 1.0, 0.0], [x.max(), x.max() - x.min(), 1.0]),
                                    maxfev=5000)
                rows.append({"pid": pid, "physical": ps,
                             "PSE": popt[0], "width": popt[1], "peak": popt[2]})
            except RuntimeError:
                pass

    df = pd.DataFrame(rows)

    if df.empty:
        print("  Warning: no individual curves converged.")
        return df

    print("\n" + "="*60)
    print("INDIVIDUAL PSE ANALYSIS")
    print("="*60)

    n_tests = df["physical"].nunique()
    alpha_corrected = 0.05 / n_tests
    print(f"  Bonferroni threshold: p < {alpha_corrected:.4f} (0.05 / {n_tests} tests)")
    print()

    for ps in sorted(df["physical"].unique()):
        pses = df[df["physical"] == ps]["PSE"].dropna()
        if len(pses) < 3:
            print(f"  phys={ps:5.0f}  n={len(pses)} (too few for t-test)")
            continue
        t, p = stats.ttest_1samp(pses, popmean=100.0)
        sig = "*" if p < 0.05 else ""
        sig_corrected = " [Bonf.*]" if p < alpha_corrected else ""
        print(f"  phys={ps:5.0f}  mean PSE={pses.mean():.1f} +/- {pses.std():.1f}"
              f"  t={t:.2f}  p={p:.3f}{sig}{sig_corrected}")

    return df


# =============================================================================
# 6. GLMM
# =============================================================================

def run_glmm_statsmodels(data):
    """
    GLMM via statsmodels (random intercept only).
    Includes direction as a fixed effect alongside visual/physical.
    """
    if not HAS_STATSMODELS:
        return None

    print("\n" + "="*60)
    print("GLMM - statsmodels (random intercept)")
    print("="*60)

    from statsmodels.genmod.bayes_mixed_glm import BinomialBayesMixedGLM

    data = data.copy()
    data["physical_str"] = "p" + data["physical"].astype(int).astype(str)

    formula = "congruent ~ visual_c * C(physical_str) + visual_c * C(direction)"
    random = {"pid": "0 + C(pid)"}

    try:
        model = BinomialBayesMixedGLM.from_formula(formula, random, data=data)
        result = model.fit_map()
        print(result.summary())
        return result
    except Exception as e:
        print(f"  statsmodels GLMM failed: {e}")
        return None


# =============================================================================
# 7. PLOTTING
# =============================================================================

def plot_psychometric_curves(curve_results, direction, save_path):
    phys_speeds = [ps for ps, r in curve_results.items() if r is not None]
    n = len(phys_speeds)
    ncols = 2
    nrows = int(np.ceil(n / ncols))

    fig, axes = plt.subplots(nrows, ncols, figsize=(11, 4 * nrows),
                             sharex=False, sharey=True)
    axes = np.atleast_1d(axes).flatten()
    colors = cm.viridis(np.linspace(0.15, 0.85, n))

    for i, ps in enumerate(phys_speeds):
        r = curve_results[ps]
        ax = axes[i]

        ax.scatter(r["x"], r["y"], color=colors[i], zorder=5, s=60, label="Observed")
        ax.plot(r["x_fine"], r["y_fine"], color=colors[i], lw=2)
        ax.axvline(r["pse"], color=colors[i], lw=1.5, linestyle="--",
                   alpha=0.8, label=f"PSE={r['pse']:.1f}")
        ax.axvline(100, color="gray", lw=1, linestyle=":", alpha=0.6,
                   label="Veridical (100)")
        if not np.isnan(r["lower_70"]):
            ax.axvspan(r["lower_70"], r["upper_70"], alpha=0.12,
                       color=colors[i], label="70% zone")
        ax.axhline(0.5, color="gray", lw=0.8, linestyle=":", alpha=0.5)
        ax.axhline(0.7, color="gray", lw=0.8, linestyle="-.", alpha=0.4)
        ax.set_title(f"Physical speed = {ps:.0f}", fontsize=11)
        ax.set_xlabel("Visual speed multiplier (%)")
        ax.set_ylabel("P(yes — match)")
        ax.set_ylim(-0.05, 1.05)
        ax.legend(fontsize=7, loc="upper left")
        ax.grid(True, alpha=0.3)

    for j in range(i + 1, len(axes)):
        axes[j].set_visible(False)

    fig.suptitle(f"Psychometric curves per physical speed — {direction}\n"
                 "(group-level proportions, bell curve fit)", fontsize=13, y=1.02)
    plt.tight_layout()
    plt.savefig(save_path, dpi=150, bbox_inches="tight")
    print(f"\nSaved: {save_path}")
    plt.close()


def plot_pse_summary(pse_dfs, reg_results, save_path="pse_summary.png"):
    """
    pse_dfs / reg_results: dicts keyed by direction ("grow", "shrink")
    """
    fig, axes = plt.subplots(1, 2, figsize=(12, 5))
    dir_colors = {"grow": "#2c7bb6", "shrink": "#d7191c"}

    ax = axes[0]
    for direction, pse_df in pse_dfs.items():
        color = dir_colors.get(direction, None)
        ax.errorbar(pse_df["physical"], pse_df["PSE"],
                    yerr=pse_df["PSE_SE"] * 1.96,
                    fmt="o-", color=color, capsize=5, lw=2, ms=7,
                    label=f"{direction} PSE ± 95% CI", zorder=3)

        reg_result = reg_results.get(direction)
        if reg_result is not None:
            x_range = np.linspace(pse_df["physical"].min(), pse_df["physical"].max(), 100)
            y_fit = reg_result["slope"] * x_range + reg_result["intercept"]
            p_val = reg_result["p"]
            p_str = f"p={p_val:.3f}" if p_val >= 0.001 else "p<0.001"
            ax.plot(x_range, y_fit, color=color, lw=1.5, linestyle="--",
                    label=f"{direction} fit (R²={reg_result['r2']:.2f}, {p_str})")

    ax.axhline(100, color="gray", lw=1.5, linestyle=":", label="Veridical (100)")
    ax.set_xlabel("Physical speed")
    ax.set_ylabel("PSE (visual multiplier %)")
    ax.set_title("Point of Subjective Equality")
    ax.legend(fontsize=8)
    ax.grid(True, alpha=0.3)

    ax = axes[1]
    for direction, pse_df in pse_dfs.items():
        color = dir_colors.get(direction, None)
        ax.plot(pse_df["physical"], pse_df["width_70"],
                "s-", color=color, lw=2, ms=7, label=f"{direction} 70% zone width")
    ax.set_xlabel("Physical speed")
    ax.set_ylabel("Acceptance zone width (visual multiplier units)")
    ax.set_title("Acceptance zone width (70%)")
    ax.legend(fontsize=8)
    ax.grid(True, alpha=0.3)

    plt.tight_layout()
    plt.savefig(save_path, dpi=150, bbox_inches="tight")
    print(f"Saved: {save_path}")
    plt.close()


def plot_2d_heatmap(data, direction, save_path):
    """Response rate as a 2D heatmap: physical speed x visual multiplier."""
    pivot = (
        data.groupby(["physical", "visual"])["congruent"]
        .mean()
        .unstack(level="visual")
    )

    fig, ax = plt.subplots(figsize=(9, 5))
    im = ax.imshow(pivot.values, aspect="auto", origin="lower",
                   cmap="RdYlGn", vmin=0, vmax=1)

    ax.set_xticks(range(len(pivot.columns)))
    ax.set_xticklabels([f"{v:.0f}" for v in pivot.columns], rotation=45)
    ax.set_yticks(range(len(pivot.index)))
    ax.set_yticklabels([f"{v:.0f}" for v in pivot.index])
    ax.set_xlabel("Visual speed multiplier (%)")
    ax.set_ylabel("Physical speed")
    ax.set_title(f"P(yes) heatmap — group average ({direction})")

    for i in range(len(pivot.index)):
        for j in range(len(pivot.columns)):
            val = pivot.values[i, j]
            if not np.isnan(val):
                ax.text(j, i, f"{val:.2f}", ha="center", va="center",
                        fontsize=8, color="black" if 0.3 < val < 0.8 else "white")

    plt.colorbar(im, ax=ax, label="P(yes)")
    plt.tight_layout()
    plt.savefig(save_path, dpi=150, bbox_inches="tight")
    print(f"Saved: {save_path}")
    plt.close()


def plot_individual_pses(ind_df, reg_result, direction, save_path):
    if ind_df.empty:
        return

    phys_speeds = sorted(ind_df["physical"].unique())

    fig, ax = plt.subplots(figsize=(8, 5))

    for i, ps in enumerate(phys_speeds):
        pses = ind_df[ind_df["physical"] == ps]["PSE"].values
        jitter = np.random.uniform(-0.15, 0.15, len(pses))
        ax.scatter([i + j for j in jitter], pses, alpha=0.5,
                   color="#2c7bb6", s=40, zorder=3)
        ax.plot(i, np.mean(pses), "D", color="#d7191c", ms=10, zorder=5)

    if reg_result is not None:
        x_idx = np.linspace(0, len(phys_speeds) - 1, 100)
        x_phys = np.linspace(phys_speeds[0], phys_speeds[-1], 100)
        y_fit = reg_result["slope"] * x_phys + reg_result["intercept"]
        p_str = f"p={reg_result['p']:.3f}" if reg_result["p"] >= 0.001 else "p<0.001"
        ax.plot(x_idx, y_fit, color="#d7191c", lw=2, linestyle="--",
                label=f"Linear fit (R²={reg_result['r2']:.2f}, {p_str})", zorder=4)

    ax.axhline(100, color="gray", lw=1.5, linestyle="--", label="Veridical (100)")
    ax.set_xticks(range(len(phys_speeds)))
    ax.set_xticklabels([f"{ps:.0f}" for ps in phys_speeds])
    ax.set_xlabel("Physical speed")
    ax.set_ylabel("Individual PSE (visual multiplier %)")
    ax.set_title(f"Individual PSEs per physical speed — {direction}\n"
                 "(blue = individual, red diamond = mean)")
    ax.legend()
    ax.grid(True, alpha=0.3)

    plt.tight_layout()
    plt.savefig(save_path, dpi=150, bbox_inches="tight")
    print(f"Saved: {save_path}")
    plt.close()


# =============================================================================
# 8. MAIN
# =============================================================================

if __name__ == "__main__":
    OUTPUT_DIR = "."

    print("Loading data...")
    data = load_all_participants(base_dir="p_sheets")
    data = prepare_data(data)

    cell_rates = descriptive_stats(data)

    pse_dfs = {}
    reg_results = {}

    for direction in sorted(data["direction"].unique()):
        print("\n" + "#"*60)
        print(f"# DIRECTION: {direction.upper()}")
        print("#"*60)

        sub = data[data["direction"] == direction]

        print("\n" + "="*60)
        print(f"PSYCHOMETRIC CURVE FITTING (group level) - {direction}")
        print("="*60)
        curve_results = fit_psychometric_curves(sub)
        pse_df = summarise_pse(curve_results)
        reg_result = pse_linear_regression(pse_df)

        ind_pse_df = fit_individual_pses(sub)
        pse_regression_individual(ind_pse_df)

        pse_dfs[direction] = pse_df
        reg_results[direction] = reg_result

        print(f"\nGenerating plots ({direction})...")
        plot_psychometric_curves(curve_results, direction,
                                 os.path.join(OUTPUT_DIR, f"psychometric_curves_{direction}.png"))
        plot_2d_heatmap(sub, direction,
                        os.path.join(OUTPUT_DIR, f"heatmap_2d_{direction}.png"))
        plot_individual_pses(ind_pse_df, reg_result, direction,
                             os.path.join(OUTPUT_DIR, f"individual_pses_{direction}.png"))

        pse_df.to_csv(os.path.join(OUTPUT_DIR, f"pse_results_{direction}.csv"), index=False)

    plot_pse_summary(pse_dfs, reg_results, os.path.join(OUTPUT_DIR, "pse_summary.png"))

    # GLMM across both directions
    glmm_result = run_glmm_statsmodels(data)

    print("\nDone. Output files:")
    print("  psychometric_curves_<direction>.png - one curve per physical speed")
    print("  heatmap_2d_<direction>.png          - full response surface")
    print("  individual_pses_<direction>.png     - per-participant PSEs with regression line")
    print("  pse_results_<direction>.csv         - numeric PSE table")
    print("  pse_summary.png                     - PSE and acceptance zone width, both directions")
