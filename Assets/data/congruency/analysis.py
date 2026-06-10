"""
Psychophysics analysis: physical size x visual multiplier matching study
- 12 participants, within-subject
- Binary response: congruent (1=yes match, 0=no match)
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

# ── Optional: pymer4 (wraps R's lme4) ────────────────────────────────────────
try:
    from pymer4.models import Lmer
    HAS_PYMER4 = True
except ImportError:
    HAS_PYMER4 = False


# =============================================================================
# 1. LOAD DATA
# =============================================================================

def load_all_participants(base_dir="p_sheets"):
    """
    Load CSVs from /p_sheets/p{N}/p{N}_conditions.csv
    Falls back to a single uploaded file if folder not found.
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
    visualSize is a multiplier where 100 = 100% = veridical match.
    """
    data = data.copy()

    # Ensure correct types
    data["pid"]          = data["pid"].astype(str)
    data["physicalSize"] = data["physicalSize"].astype(float)
    data["visualSize"]   = data["visualSize"].astype(float)

    # Exclude participants whose 'congruent' responses are mostly missing
    # (e.g. p13/pid 12, where recording failed for almost the whole session)
    nan_counts = data.groupby("pid")["congruent"].apply(lambda s: s.isna().sum())
    excluded_pids = nan_counts[nan_counts > 0.5 * data.groupby("pid").size().max()].index
    if len(excluded_pids) > 0:
        print(f"Excluding participants with mostly missing 'congruent' data: {list(excluded_pids)}")
        data = data[~data["pid"].isin(excluded_pids)]

    # Drop any remaining rows with missing 'congruent' values
    n_missing = data["congruent"].isna().sum()
    if n_missing > 0:
        print(f"Dropping {n_missing} row(s) with missing 'congruent' values")
        data = data.dropna(subset=["congruent"])

    data["congruent"]    = data["congruent"].astype(int)

    # Log ratio: 0 = perfect veridical match, positive = visual larger
    data["log_ratio"] = np.log(data["visualSize"] / 100.0)

    # Centered versions for better model convergence
    data["visual_c"]   = (data["visualSize"]   - data["visualSize"].mean())   / data["visualSize"].std()
    data["physical_c"] = (data["physicalSize"]  - data["physicalSize"].mean()) / data["physicalSize"].std()

    # Physical size as ordered category
    data["physicalSize_cat"] = pd.Categorical(
        data["physicalSize"],
        categories=sorted(data["physicalSize"].unique()),
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
    print(f"Participants : {data['pid'].nunique()}")
    print(f"Physical sizes: {sorted(data['physicalSize'].unique())}")
    print(f"Visual sizes  : {sorted(data['visualSize'].unique())}")
    print(f"Trials per participant: {data.groupby('pid').size().values}")
    print(f"Overall 'yes' rate: {data['congruent'].mean():.3f}")

    # Response rate per condition cell
    cell_rates = (
        data.groupby(["physicalSize", "visualSize"])["congruent"]
        .agg(["mean", "count"])
        .rename(columns={"mean": "p_yes", "count": "n"})
        .reset_index()
    )
    print("\nResponse rates (first 10 rows):")
    print(cell_rates.head(10).to_string(index=False))
    return cell_rates


# =============================================================================
# 3. PSYCHOMETRIC CURVE FITTING (per physical size, group-level)
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
    Aggregate across participants per (physicalSize, visualSize) cell,
    then fit a logistic curve per physical size.
    Returns a dict: physicalSize -> {pse, width, lower_70, upper_70, ...}
    """
    cell = (
        data.groupby(["physicalSize", "visualSize"])["congruent"]
        .mean()
        .reset_index()
    )

    results = {}
    phys_sizes = sorted(cell["physicalSize"].unique())

    for ps in phys_sizes:
        sub = cell[cell["physicalSize"] == ps].sort_values("visualSize")
        x = sub["visualSize"].values
        y = sub["congruent"].values

        try:
            # Initial guess: PSE near 100 (veridical), moderate width, peak near max observed
            p0 = [100.0, 30.0, y.max()]
            bounds = ([x.min(), 1.0, 0.0], [x.max(), 200.0, 1.0])
            popt, pcov = curve_fit(bell_curve, x, y, p0=p0, bounds=bounds, maxfev=5000)
            pse, width, peak = popt
            perr = np.sqrt(np.diag(pcov))

            # Threshold crossings: find visual sizes where p(yes) = threshold
            x_fine = np.linspace(x.min(), x.max(), 10000)
            y_fine = bell_curve(x_fine, pse, width, peak)

            def threshold_crossing(thresh):
                # Returns (lower, upper) crossings around PSE
                above = x_fine[y_fine >= thresh]
                return (above[0] if len(above) > 0 else np.nan,
                        above[-1] if len(above) > 0 else np.nan)

            l50, u50 = threshold_crossing(0.50)
            l70, u70 = threshold_crossing(0.70)
            l75, u75 = threshold_crossing(0.75)

            results[ps] = {
                "pse": pse,
                "width": width,
                "peak": peak,
                "pse_se": perr[0],
                "width_se": perr[1],
                "peak_se": perr[2],
                "lower_50": l50, "upper_50": u50,
                "lower_70": l70, "upper_70": u70,
                "lower_75": l75, "upper_75": u75,
                "width_70": u70 - l70,
                "width_75": u75 - l75,
                "bias": pse - 100.0,          # positive = overestimate visual
                "x": x, "y": y,
                "x_fine": x_fine, "y_fine": y_fine,
            }
            print(f"  phys={ps:5.0f}  PSE={pse:6.1f}  width={width:5.1f}  peak={peak:4.2f}"
                  f"  bias={pse-100:+.1f}  70%-zone=[{l70:.1f}, {u70:.1f}]")

        except RuntimeError as e:
            print(f"  phys={ps}: curve fit failed — {e}")
            results[ps] = None

    return results


def summarise_pse(curve_results):
    rows = []
    for ps, r in curve_results.items():
        if r is None:
            continue
        rows.append({
            "physicalSize": ps,
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
# 4. INDIVIDUAL PSEs (per participant x physical size)
# =============================================================================

def fit_individual_pses(data):
    """
    Fit per-participant psychometric curves.
    With only 5 reps, curves will be noisy — use with caution.
    Returns a DataFrame of individual PSEs for group-level inference.
    """
    rows = []
    for pid in sorted(data["pid"].unique()):
        for ps in sorted(data["physicalSize"].unique()):
            sub = (
                data[(data["pid"] == pid) & (data["physicalSize"] == ps)]
                .groupby("visualSize")["congruent"]
                .mean()
                .reset_index()
                .sort_values("visualSize")
            )
            if len(sub) < 3:
                continue
            x, y = sub["visualSize"].values, sub["congruent"].values
            try:
                popt, _ = curve_fit(bell_curve, x, y,
                                    p0=[100.0, 30.0, y.max()],
                                    bounds=([x.min(), 1.0, 0.0], [x.max(), 200.0, 1.0]),
                                    maxfev=5000)
                rows.append({"pid": pid, "physicalSize": ps,
                             "PSE": popt[0], "width": popt[1], "peak": popt[2]})
            except RuntimeError:
                pass

    df = pd.DataFrame(rows)

    if df.empty:
        print("  Warning: no individual curves converged.")
        return df

    # One-sample t-test: is group PSE different from 100 (veridical)?
    print("\n" + "="*60)
    print("INDIVIDUAL PSE ANALYSIS")
    print("="*60)
    for ps in sorted(df["physicalSize"].unique()):
        pses = df[df["physicalSize"] == ps]["PSE"].dropna()
        if len(pses) < 3:
            continue
        t, p = stats.ttest_1samp(pses, popmean=100.0)
        print(f"  phys={ps:5.0f}  mean PSE={pses.mean():.1f} ± {pses.std():.1f}"
              f"  t={t:.2f}  p={p:.3f}{'*' if p < 0.05 else ''}")

    return df


# =============================================================================
# 5. GLMM
# =============================================================================

def run_glmm_statsmodels(data):
    """
    GLMM via statsmodels (BinomialBayesMixedGLM or MixedLM approximation).
    Note: statsmodels GLMM support is limited — random intercept only.
    For random slopes, use pymer4 (see below).
    """
    if not HAS_STATSMODELS:
        return None

    print("\n" + "="*60)
    print("GLMM — statsmodels (random intercept)")
    print("="*60)

    from statsmodels.genmod.bayes_mixed_glm import BinomialBayesMixedGLM

    # physicalSize as categorical
    data = data.copy()
    data["physicalSize_str"] = "p" + data["physicalSize"].astype(int).astype(str)

    formula = "congruent ~ visual_c * C(physicalSize_str)"
    random = {"pid": "0 + C(pid)"}   # random intercept per participant

    try:
        model = BinomialBayesMixedGLM.from_formula(
            formula, random, data=data
        )
        result = model.fit_map()
        print(result.summary())
        return result
    except Exception as e:
        print(f"  statsmodels GLMM failed: {e}")
        print("  Try pymer4 for full random slopes.")
        return None


def run_glmm_pymer4(data):
    """
    Full GLMM via pymer4 (requires R + lme4).
    Model: congruent ~ visual_c * physical_c + (1 + visual_c | pid)
    """
    if not HAS_PYMER4:
        print("\npymer4 not available. Install with: pip install pymer4")
        print("Also requires R with lme4: install.packages('lme4')")
        return None

    print("\n" + "="*60)
    print("GLMM — pymer4 / lme4 (random intercept + slope)")
    print("="*60)

    # Full model with random slope
    formula = "congruent ~ visual_c * physical_c + (1 + visual_c | pid)"
    model = Lmer(formula, data=data, family="binomial")

    try:
        result = model.fit()
        print(result)

        # If convergence fails, fall back to random intercept only
    except Exception as e:
        print(f"  Full model failed ({e}), trying random intercept only...")
        formula_ri = "congruent ~ visual_c * physical_c + (1 | pid)"
        model = Lmer(formula_ri, data=data, family="binomial")
        result = model.fit()
        print(result)

    return model


# =============================================================================
# 6. PLOTTING
# =============================================================================

def plot_psychometric_curves(curve_results, save_path="psychometric_curves.png"):
    phys_sizes = [ps for ps, r in curve_results.items() if r is not None]
    n = len(phys_sizes)
    ncols = 3
    nrows = int(np.ceil(n / ncols))

    fig, axes = plt.subplots(nrows, ncols, figsize=(14, 4 * nrows),
                             sharex=False, sharey=True)
    axes = axes.flatten()
    colors = cm.viridis(np.linspace(0.15, 0.85, n))

    for i, ps in enumerate(phys_sizes):
        r = curve_results[ps]
        ax = axes[i]

        # Data points (group-level proportions)
        ax.scatter(r["x"], r["y"], color=colors[i], zorder=5,
                   s=60, label="Observed")

        # Fitted curve
        ax.plot(r["x_fine"], r["y_fine"], color=colors[i], lw=2)

        # PSE line
        ax.axvline(r["pse"], color=colors[i], lw=1.5, linestyle="--",
                   alpha=0.8, label=f"PSE={r['pse']:.1f}")

        # Veridical line
        ax.axvline(100, color="gray", lw=1, linestyle=":", alpha=0.6,
                   label="Veridical (100)")

        # 70% acceptance zone
        if not np.isnan(r["lower_70"]):
            ax.axvspan(r["lower_70"], r["upper_70"], alpha=0.12,
                       color=colors[i], label=f"70% zone")

        # 50% threshold line
        ax.axhline(0.5, color="gray", lw=0.8, linestyle=":", alpha=0.5)
        ax.axhline(0.7, color="gray", lw=0.8, linestyle="-.", alpha=0.4)

        ax.set_title(f"Physical size = {ps:.0f}", fontsize=11)
        ax.set_xlabel("Visual multiplier (%)")
        ax.set_ylabel("P(yes — match)")
        ax.set_ylim(-0.05, 1.05)
        ax.legend(fontsize=7, loc="upper left")
        ax.grid(True, alpha=0.3)

    # Hide unused subplots
    for j in range(i + 1, len(axes)):
        axes[j].set_visible(False)

    fig.suptitle("Psychometric curves per physical size\n"
                 "(group-level proportions, logistic fit)", fontsize=13, y=1.01)
    plt.tight_layout()
    plt.savefig(save_path, dpi=150, bbox_inches="tight")
    print(f"\nSaved: {save_path}")
    plt.close()


def plot_pse_summary(pse_df, save_path="pse_summary.png"):
    fig, axes = plt.subplots(1, 2, figsize=(12, 5))

    # PSE with error bars
    ax = axes[0]
    ax.errorbar(pse_df["physicalSize"], pse_df["PSE"],
                yerr=pse_df["PSE_SE"] * 1.96,
                fmt="o-", color="#2c7bb6", capsize=5, lw=2, ms=7,
                label="PSE ± 95% CI")
    ax.axhline(100, color="gray", lw=1.5, linestyle="--", label="Veridical (100)")
    ax.set_xlabel("Physical size")
    ax.set_ylabel("PSE (visual multiplier %)")
    ax.set_title("Point of Subjective Equality")
    ax.legend()
    ax.grid(True, alpha=0.3)

    # Acceptance zone width (70%)
    ax = axes[1]
    ax.plot(pse_df["physicalSize"], pse_df["width_70"],
            "s-", color="#d7191c", lw=2, ms=7, label="70% zone width")
    ax.plot(pse_df["physicalSize"], pse_df["width_75"],
            "^-", color="#fdae61", lw=2, ms=7, label="75% zone width")
    ax.set_xlabel("Physical size")
    ax.set_ylabel("Acceptance zone width (visual multiplier units)")
    ax.set_title("Perceptual tolerance (JND proxy)")
    ax.legend()
    ax.grid(True, alpha=0.3)

    plt.tight_layout()
    plt.savefig(save_path, dpi=150, bbox_inches="tight")
    print(f"Saved: {save_path}")
    plt.close()


def plot_2d_heatmap(data, save_path="heatmap_2d.png"):
    """Response rate as a 2D heatmap: physical size x visual multiplier."""
    pivot = (
        data.groupby(["physicalSize", "visualSize"])["congruent"]
        .mean()
        .unstack(level="visualSize")
    )

    fig, ax = plt.subplots(figsize=(11, 6))
    im = ax.imshow(pivot.values, aspect="auto", origin="lower",
                   cmap="RdYlGn", vmin=0, vmax=1)

    ax.set_xticks(range(len(pivot.columns)))
    ax.set_xticklabels([f"{v:.0f}" for v in pivot.columns], rotation=45)
    ax.set_yticks(range(len(pivot.index)))
    ax.set_yticklabels([f"{v:.0f}" for v in pivot.index])
    ax.set_xlabel("Visual multiplier (%)")
    ax.set_ylabel("Physical size")
    ax.set_title("P(yes) heatmap — group average")

    # Annotate cells
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


def plot_individual_pses(ind_df, save_path="individual_pses.png"):
    if ind_df.empty:
        return

    phys_sizes = sorted(ind_df["physicalSize"].unique())
    pse_by_size = [ind_df[ind_df["physicalSize"] == ps]["PSE"].values
                   for ps in phys_sizes]

    fig, ax = plt.subplots(figsize=(10, 5))

    # Individual points
    for i, (ps, pses) in enumerate(zip(phys_sizes, pse_by_size)):
        jitter = np.random.uniform(-0.15, 0.15, len(pses))
        ax.scatter([i + j for j in jitter], pses, alpha=0.5,
                   color="#2c7bb6", s=40, zorder=3)
        ax.plot(i, np.mean(pses), "D", color="#d7191c", ms=10, zorder=5)

    ax.axhline(100, color="gray", lw=1.5, linestyle="--", label="Veridical (100)")
    ax.set_xticks(range(len(phys_sizes)))
    ax.set_xticklabels([f"{ps:.0f}" for ps in phys_sizes])
    ax.set_xlabel("Physical size")
    ax.set_ylabel("Individual PSE (visual multiplier %)")
    ax.set_title("Individual PSEs per physical size\n(blue = individual, red = mean)")
    ax.legend()
    ax.grid(True, alpha=0.3)

    plt.tight_layout()
    plt.savefig(save_path, dpi=150, bbox_inches="tight")
    print(f"Saved: {save_path}")
    plt.close()


# =============================================================================
# 7. MAIN
# =============================================================================

if __name__ == "__main__":
    OUTPUT_DIR = "."   # change to save plots elsewhere

    # ── Load ──────────────────────────────────────────────────────────────────
    print("Loading data...")
    data = load_all_participants(base_dir="p_sheets")
    data = prepare_data(data)

    # ── Descriptives ──────────────────────────────────────────────────────────
    cell_rates = descriptive_stats(data)

    # ── Group-level psychometric curves ───────────────────────────────────────
    print("\n" + "="*60)
    print("PSYCHOMETRIC CURVE FITTING (group level)")
    print("="*60)
    curve_results = fit_psychometric_curves(data)
    pse_df = summarise_pse(curve_results)

    # ── Individual PSEs ───────────────────────────────────────────────────────
    ind_pse_df = fit_individual_pses(data)

    # ── GLMM ──────────────────────────────────────────────────────────────────
    # Option A: statsmodels (no R needed, random intercept only)
    glmm_result = run_glmm_statsmodels(data)

    # Option B: pymer4/lme4 (requires R, full random slopes)
    # glmm_result = run_glmm_pymer4(data)

    # ── Plots ─────────────────────────────────────────────────────────────────
    print("\nGenerating plots...")
    plot_psychometric_curves(curve_results,
                             os.path.join(OUTPUT_DIR, "psychometric_curves.png"))
    plot_pse_summary(pse_df,
                     os.path.join(OUTPUT_DIR, "pse_summary.png"))
    plot_2d_heatmap(data,
                    os.path.join(OUTPUT_DIR, "heatmap_2d.png"))
    plot_individual_pses(ind_pse_df,
                         os.path.join(OUTPUT_DIR, "individual_pses.png"))

    # ── Save results table ────────────────────────────────────────────────────
    pse_df.to_csv(os.path.join(OUTPUT_DIR, "pse_results.csv"), index=False)
    print("\nDone. Output files:")
    print("  psychometric_curves.png — one curve per physical size")
    print("  pse_summary.png         — PSE and acceptance zone width")
    print("  heatmap_2d.png          — full response surface")
    print("  individual_pses.png     — per-participant PSEs")
    print("  pse_results.csv         — numeric PSE table")
