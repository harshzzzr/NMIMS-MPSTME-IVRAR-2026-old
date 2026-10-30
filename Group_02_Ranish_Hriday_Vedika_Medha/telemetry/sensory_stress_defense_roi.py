"""Evidence-aware analysis for Group 02 empirical trials.

This module never synthesizes observations or overwrites the source CSV. Supply a
CSV collected from real sessions using --input. Economics are computed only when
the user supplies the corresponding cost inputs.
"""
import argparse
from pathlib import Path

import matplotlib.pyplot as plt
import pandas as pd
from scipy import stats


def paired_latency_analysis(df, control="Control", intervention="VR", minimum_n=10):
    required = {"participant_id", "condition", "reaction_latency_ms"}
    missing = required - set(df.columns)
    if missing:
        raise ValueError("Missing paired-analysis columns: " + ", ".join(sorted(missing)))
    selected = df[df["condition"].isin([control, intervention])].copy()
    selected["reaction_latency_ms"] = pd.to_numeric(selected["reaction_latency_ms"], errors="coerce")
    selected = selected.dropna(subset=["participant_id", "reaction_latency_ms"])
    means = selected.groupby(["participant_id", "condition"])["reaction_latency_ms"].mean().unstack()
    if control not in means or intervention not in means:
        raise ValueError("Both requested conditions must occur in the input data.")
    paired = means[[control, intervention]].dropna()
    if len(paired) < minimum_n:
        raise ValueError(f"At least {minimum_n} participants with both conditions are required for the planned inferential analysis; found {len(paired)}.")
    differences = paired[control] - paired[intervention]
    result = stats.ttest_rel(paired[control], paired[intervention])
    sd = differences.std(ddof=1)
    margin = stats.t.ppf(0.975, len(differences) - 1) * stats.sem(differences)
    return {
        "n_paired": int(len(paired)), "control_mean_ms": float(paired[control].mean()),
        "intervention_mean_ms": float(paired[intervention].mean()),
        "reduction_percent": float(100 * differences.mean() / paired[control].mean()) if paired[control].mean() else float("nan"),
        "t_statistic": float(result.statistic), "p_value": float(result.pvalue),
        "degrees_of_freedom": int(len(differences) - 1),
        "mean_paired_difference_ms": float(differences.mean()),
        "mean_difference_95ci_ms": [float(differences.mean() - margin), float(differences.mean() + margin)],
        "cohens_dz": float(differences.mean() / sd) if sd else float("nan"),
        "participant_means": paired,
    }


def sdt_from_trials(df):
    """Return log-linear corrected SDT metrics from target/action trial rows."""
    required = {"target_type", "trainee_action"}
    missing = required - set(df.columns)
    if missing:
        raise ValueError("Missing SDT columns: " + ", ".join(sorted(missing)))
    hostile = df["target_type"].astype(str).str.lower().eq("hostile")
    engaged = df["trainee_action"].astype(str).str.lower().eq("engaged")
    hits = int((hostile & engaged).sum())
    misses = int((hostile & ~engaged).sum())
    false_alarms = int((~hostile & engaged).sum())
    correct_rejections = int((~hostile & ~engaged).sum())
    signal_n, noise_n = hits + misses, false_alarms + correct_rejections
    if not signal_n or not noise_n:
        raise ValueError("SDT needs at least one hostile and one civilian target response.")
    hit_rate = (hits + 0.5) / (signal_n + 1)
    fa_rate = (false_alarms + 0.5) / (noise_n + 1)
    z_hit, z_fa = stats.norm.ppf(hit_rate), stats.norm.ppf(fa_rate)
    d_prime = z_hit - z_fa
    criterion = -0.5 * (z_hit + z_fa)
    return {"hits": hits, "misses": misses, "false_alarms": false_alarms,
            "correct_rejections": correct_rejections, "hit_rate_corrected": float(hit_rate),
            "false_alarm_rate_corrected": float(fa_rate), "d_prime": float(d_prime),
            "criterion_c": float(criterion), "beta": float(__import__("math").exp(-d_prime * criterion))}


def economics(op_ex_vr, op_ex_physical, capex, annual_savings):
    if op_ex_physical <= 0 or annual_savings <= 0 or capex < 0:
        raise ValueError("Physical OpEx and annual savings must be positive; CapEx cannot be negative.")
    kappa = op_ex_vr / op_ex_physical
    return {"kappa": kappa, "operational_savings_percent": (1 - kappa) * 100,
            "payback_months": capex / annual_savings * 12}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", type=Path, help="Path to a real empirical CSV; source file is read-only.")
    parser.add_argument("--control", default="Control")
    parser.add_argument("--intervention", default="VR")
    parser.add_argument("--minimum-participants", type=int, default=10)
    parser.add_argument("--op-ex-vr", type=float)
    parser.add_argument("--op-ex-physical", type=float)
    parser.add_argument("--capex", type=float)
    parser.add_argument("--annual-savings", type=float)
    parser.add_argument("--figures-dir", type=Path, default=Path("docs/figures"))
    args = parser.parse_args()

    if args.input is None:
        print("No empirical dataset supplied. No synthetic data was generated and no results or figures were written.")
        print("For paired analysis provide participant_id, condition, and reaction_latency_ms columns.")
        return
    df = pd.read_csv(args.input)
    aliases = {
        "participantid": "participant_id", "sessionid": "session_id", "trialid": "trial_id",
        "latencyms": "reaction_latency_ms", "conditionlabel": "condition", "targettype": "target_type",
        "traineeaction": "trainee_action", "datastatus": "data_status",
    }
    df = df.rename(columns={column: aliases.get(column.replace("_", "").lower(), column) for column in df.columns})
    if "data_status" not in df.columns:
        raise ValueError("Input must contain data_status=empirical on every row; provenance is required.")
    if not df["data_status"].astype(str).str.lower().eq("empirical").all():
        raise ValueError("Input contains rows not explicitly marked data_status=empirical; refusing inferential analysis.")
    print("EMPIRICAL INPUT:", args.input.resolve())
    if {"participant_id", "condition", "reaction_latency_ms"}.issubset(df.columns):
        result = paired_latency_analysis(df, args.control, args.intervention, args.minimum_participants)
        for key, value in result.items():
            if key != "participant_means": print(f"{key}: {value}")
        ax = result["participant_means"].plot(kind="box", title="Participant mean reaction latency (empirical)", ylabel="Latency (ms)")
        ax.figure.tight_layout()
        args.figures_dir.mkdir(parents=True, exist_ok=True)
        ax.figure.savefig(args.figures_dir / "empirical_reaction_latency.png", dpi=300)
        plt.close(ax.figure)
    if {"target_type", "trainee_action"}.issubset(df.columns):
        print("SDT:", sdt_from_trials(df))
    economics_args = (args.op_ex_vr, args.op_ex_physical, args.capex, args.annual_savings)
    if any(value is not None for value in economics_args):
        if any(value is None for value in economics_args):
            raise ValueError("Provide all four economics inputs: --op-ex-vr, --op-ex-physical, --capex, --annual-savings.")
        print("ECONOMICS FROM USER-SUPPLIED COST INPUTS:", economics(*economics_args))
    print("Note: trial-level rows are not automatically independent participants; paired tests use per-participant condition means.")


if __name__ == "__main__":
    main()
