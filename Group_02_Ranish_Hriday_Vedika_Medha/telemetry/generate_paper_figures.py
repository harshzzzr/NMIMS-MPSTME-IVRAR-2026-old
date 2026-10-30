# =====================================================================================
# NOTICE TO STUDENTS:
# The functions in this script generate SYNTHETIC/SIMULATED reference data for demonstration purposes only.
# Per Section 4 of your IVRAR Technical Dossier, you MUST conduct real human user trials (N >= 10),
# log actual telemetry/SUS data, and replace simulated numbers with your genuine experimental results.
# Submitting simulated data as real experimental findings violates NMIMS academic integrity policies.
# =====================================================================================
import os
import csv
import numpy as np
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt

base_dir = os.path.dirname(os.path.abspath(__file__))
group_dir = os.path.dirname(base_dir)
# Never put synthetic figures in the publication figure directory.
figures_dir = os.path.join(base_dir, 'SIMULATED_figures')
os.makedirs(figures_dir, exist_ok=True)

# 1. Generate a synthetic demonstration fixture (not participant data)
np.random.seed(42)
n_trials = 50
csv_path = os.path.join(base_dir, 'SIMULATED_security_training_benchmark.csv')

fieldnames = [
    'trial_id',
    'trainee_cohort',
    'ambient_lux',
    'auditory_stress_db',
    'target_type',  # Hostile vs Non-Hostile
    'decision_outcome', # Hit, Miss, False_Alarm, Correct_Rejection
    'reaction_latency_ms',
    'aim_accuracy_score',
    'nasa_tlx_score',
    'heart_rate_bpm',
    'sagat_score'
]

rows = []
cohorts = ['Novice_Control', 'VR_Stress_Inoculated']

for i in range(1, n_trials + 1):
    cohort = cohorts[i % 2]
    lux = np.round(np.random.uniform(0.5, 5.0), 2) # Nighttime low visibility
    stress_db = np.round(np.random.uniform(70.0, 95.0), 1) # Gunfire & siren noise
    is_hostile = np.random.choice([True, False], p=[0.6, 0.4])
    target_str = 'Hostile' if is_hostile else 'Non-Hostile'
    
    if cohort == 'VR_Stress_Inoculated':
        # Trained under VR stress: faster reaction, fewer false alarms
        latency = np.round(np.random.normal(480.0, 45.0), 1)
        tlx = np.round(np.random.normal(42.0, 6.0), 1)
        hr = int(np.random.normal(108, 8))
        sagat = np.round(np.random.uniform(78.0, 95.0), 1)
        accuracy = np.round(np.random.uniform(85.0, 98.0), 1)
        if is_hostile:
            outcome = 'Hit' if np.random.rand() < 0.94 else 'Miss'
        else:
            outcome = 'False_Alarm' if np.random.rand() < 0.06 else 'Correct_Rejection'
    else:
        # Untrained control: slower reaction, higher cognitive load, more false alarms
        latency = np.round(np.random.normal(740.0, 85.0), 1)
        tlx = np.round(np.random.normal(68.0, 9.0), 1)
        hr = int(np.random.normal(136, 12))
        sagat = np.round(np.random.uniform(50.0, 72.0), 1)
        accuracy = np.round(np.random.uniform(62.0, 84.0), 1)
        if is_hostile:
            outcome = 'Hit' if np.random.rand() < 0.78 else 'Miss'
        else:
            outcome = 'False_Alarm' if np.random.rand() < 0.28 else 'Correct_Rejection'

    rows.append({
        'trial_id': f'TRIAL_{i:03d}',
        'trainee_cohort': cohort,
        'ambient_lux': lux,
        'auditory_stress_db': stress_db,
        'target_type': target_str,
        'decision_outcome': outcome,
        'reaction_latency_ms': latency,
        'aim_accuracy_score': accuracy,
        'nasa_tlx_score': tlx,
        'heart_rate_bpm': hr,
        'sagat_score': sagat
    })

with open(csv_path, 'w', newline='', encoding='utf-8') as f:
    writer = csv.DictWriter(f, fieldnames=fieldnames)
    writer.writeheader()
    writer.writerows(rows)
print(f'[SUCCESS] Generated benchmark dataset: {csv_path}')

# Figure styling for publication
plt.rcParams['font.sans-serif'] = 'DejaVu Sans'
plt.rcParams['axes.edgecolor'] = '#333333'
plt.rcParams['axes.linewidth'] = 1.0

# -------------------------------------------------------------
# FIGURE 1: System Architecture & Defensive Scenario Pipeline
# -------------------------------------------------------------
fig1, ax1 = plt.subplots(figsize=(10, 5.5), dpi=300)
ax1.set_xlim(0, 10)
ax1.set_ylim(0, 6)
ax1.axis('off')

# Diagram blocks
boxes = [
    (0.5, 3.8, 2.5, 1.6, '#E3F2FD', '#1565C0', 'Interactive 3D VR Scene\n- Unity OpenXR / Low-Lux\n- Volumetric Nighttime Fog\n- Dynamic Flashlight Cone'),
    (3.8, 3.8, 2.5, 1.6, '#FFF3E0', '#E65100', 'Sensory Stress Engine\n- 85-95 dB Auditory Startle\n- Peripheral Glare Flashes\n- Shoot/Don\'t-Shoot Targets'),
    (7.1, 3.8, 2.4, 1.6, '#E8F5E9', '#2E7D32', 'Spatial Telemetry Engine\n- Reaction Latency (ms)\n- Signal Detection (d\', beta)\n- NASA-TLX & SAGAT Logs'),
    (2.0, 0.8, 3.0, 1.6, '#F3E5F5', '#7B1FA2', 'Cognitive Workload Analysis\n- Inoculation Efficacy Curve\n- Heart Rate & Stress Proxy\n- Error Rate Attenuation'),
    (6.0, 0.8, 3.0, 1.6, '#ECEFF1', '#37474F', 'Techno-Managerial Model\n- Live-Fire Ammo Savings\n- Shoot-House Hours Replaced\n- Amortization & Payback')
]

for x, y, w, h, bg, border, label in boxes:
    rect = plt.Rectangle((x, y), w, h, facecolor=bg, edgecolor=border, linewidth=2, linestyle='-', zorder=2)
    ax1.add_patch(rect)
    ax1.text(x + w/2, y + h/2, label, ha='center', va='center', fontsize=9, fontweight='bold', color='#1A1A1A', zorder=3)

# Connectors
arrows = [
    (3.0, 4.6, 3.8, 4.6),
    (6.3, 4.6, 7.1, 4.6),
    (5.05, 3.8, 3.5, 2.4),
    (8.3, 3.8, 7.5, 2.4),
    (5.0, 1.6, 6.0, 1.6)
]

for x1, y1, x2, y2 in arrows:
    ax1.annotate('', xy=(x2, y2), xytext=(x1, y1),
                 arrowprops=dict(arrowstyle='->', color='#263238', lw=1.8, shrinkA=2, shrinkB=2))

ax1.set_title('Figure 1: IVRAR Group 02 - Immersive Sensory-Stress Training & Telemetry Architecture',
              fontsize=11, fontweight='bold', pad=12)
fig1.tight_layout()
fig1_path = os.path.join(figures_dir, 'SIMULATED_figure1_system_architecture.png')
fig1.text(0.5, 0.015, 'SIMULATED / ILLUSTRATIVE OUTPUT — NOT PARTICIPANT DATA', ha='center', color='darkred', fontsize=8)
fig1.savefig(fig1_path, dpi=300)
plt.close(fig1)
print(f'[SUCCESS] Generated Figure 1: {fig1_path}')

# -------------------------------------------------------------
# FIGURE 2: Trainee Reaction Latency & Signal Detection Dynamics
# -------------------------------------------------------------
fig2, (ax2a, ax2b) = plt.subplots(1, 2, figsize=(11, 4.8), dpi=300)

# 2a: Reaction Latency Distribution across lighting/lux
lux_levels = np.linspace(0.5, 5.0, 30)
latency_control = 820 - 45 * lux_levels + np.random.normal(0, 15, 30)
latency_inoculated = 540 - 22 * lux_levels + np.random.normal(0, 10, 30)

ax2a.plot(lux_levels, latency_control, 'r-o', markersize=5, label='Untrained Control Cohort')
ax2a.plot(lux_levels, latency_inoculated, 'b-s', markersize=5, label='VR Stress-Inoculated Cohort')
ax2a.set_xlabel('Ambient Illumination (Lux)', fontsize=10)
ax2a.set_ylabel('Mean Reaction Latency (ms)', fontsize=10)
ax2a.set_title('(a) Reaction Latency vs. Ambient Lighting', fontsize=10, fontweight='bold')
ax2a.grid(True, linestyle='--', alpha=0.6)
ax2a.legend(frameon=True, facecolor='white', edgecolor='none')

# 2b: Signal Detection ROC curves (Hit vs False Alarm)
p_fa = np.linspace(0.01, 0.99, 100)
from scipy.stats import norm
roc_control = norm.cdf(norm.ppf(p_fa) + 1.2)
roc_inoculated = norm.cdf(norm.ppf(p_fa) + 2.8)

ax2b.plot(p_fa, roc_inoculated, 'b-', linewidth=2.5, label='VR Inoculated (d\' = 2.82)')
ax2b.plot(p_fa, roc_control, 'r--', linewidth=2.0, label='Control Cohort (d\' = 1.24)')
ax2b.plot([0, 1], [0, 1], 'k:', alpha=0.5, label='Chance Line (d\' = 0)')
ax2b.scatter([0.06], [0.94], color='blue', s=70, zorder=5, label='Inoculated Operating Point')
ax2b.scatter([0.28], [0.78], color='red', s=70, zorder=5, label='Control Operating Point')
ax2b.set_xlabel('False Alarm Rate (Engaging Civilian)', fontsize=10)
ax2b.set_ylabel('Hit Rate (Neutralizing Threat)', fontsize=10)
ax2b.set_title('(b) Signal Detection ROC & Threat Sensitivity', fontsize=10, fontweight='bold')
ax2b.grid(True, linestyle='--', alpha=0.6)
ax2b.legend(loc='lower right', frameon=True, facecolor='white')

fig2.suptitle('SIMULATED EXAMPLE — NOT EMPIRICAL DATA', fontsize=12, fontweight='bold', color='darkred')
fig2.tight_layout()
fig2_path = os.path.join(figures_dir, 'SIMULATED_figure2_kinematic_telemetry.png')
fig2.text(0.5, 0.015, 'SIMULATED / ILLUSTRATIVE OUTPUT — NOT PARTICIPANT DATA', ha='center', color='darkred', fontsize=8)
fig2.savefig(fig2_path, dpi=300)
plt.close(fig2)
print(f'[SUCCESS] Generated Figure 2: {fig2_path}')

# -------------------------------------------------------------
# FIGURE 3: Comparative Performance, Workload & Training Economics
# -------------------------------------------------------------
fig3, (ax3a, ax3b) = plt.subplots(1, 2, figsize=(11, 4.8), dpi=300)

# 3a: NASA-TLX Subscales Comparison
subscales = ['Mental', 'Physical', 'Temporal', 'Performance', 'Effort', 'Frustration']
control_tlx = [78, 62, 84, 52, 76, 68]
inoculated_tlx = [46, 38, 48, 88, 44, 32]

x = np.arange(len(subscales))
width = 0.35

ax3a.bar(x - width/2, control_tlx, width, label='Untrained Control', color='#EF5350', edgecolor='#C62828')
ax3a.bar(x + width/2, inoculated_tlx, width, label='VR Stress Inoculated', color='#42A5F5', edgecolor='#1565C0')
ax3a.set_ylabel('Subscale Rating (0 - 100)', fontsize=10)
ax3a.set_title('(a) NASA-TLX Cognitive Workload Breakdown', fontsize=10, fontweight='bold')
ax3a.set_xticks(x)
ax3a.set_xticklabels(subscales, rotation=25, ha='right', fontsize=9)
ax3a.grid(True, axis='y', linestyle='--', alpha=0.6)
ax3a.legend(loc='upper right')

# 3b: Amortized Training Cost Parity
hours = np.linspace(10, 250, 50)
cost_ratio = (25000 + 45 * hours) / (120000 + 480 * hours)

ax3b.plot(hours, cost_ratio, 'g-', linewidth=2.5, label='Cost Parity Ratio (VR / Live-Fire)')
ax3b.axhline(0.25, color='gray', linestyle='--', label='75% Operational Savings Benchmark')
ax3b.scatter([65.0], [0.35], color='darkgreen', s=60, zorder=5)
ax3b.annotate('Payback Threshold (~65 Trainee Hours)', xy=(65.0, 0.35), xytext=(80, 0.48),
             arrowprops=dict(arrowstyle='->', lw=1.2))
ax3b.set_xlabel('Cumulative Training Throughput (Trainee Hours)', fontsize=10)
ax3b.set_ylabel('Cost Ratio (Dimensionless)', fontsize=10)
ax3b.set_title('(b) Technoeconomic Operational Amortization', fontsize=10, fontweight='bold')
ax3b.grid(True, linestyle='--', alpha=0.6)
ax3b.legend(loc='upper right')

fig3.suptitle('Figure 3: Comparative Human Factors & Technoeconomic Benchmarks', fontsize=12, fontweight='bold')
fig3.tight_layout()
fig3_path = os.path.join(figures_dir, 'SIMULATED_figure3_comparative_performance.png')
fig3.text(0.5, 0.015, 'SIMULATED / ILLUSTRATIVE OUTPUT — NOT PARTICIPANT DATA', ha='center', color='darkred', fontsize=8)
fig3.savefig(fig3_path, dpi=300)
plt.close(fig3)
print(f'[SUCCESS] Generated Figure 3: {fig3_path}')
