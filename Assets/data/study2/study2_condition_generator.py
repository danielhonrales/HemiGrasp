import itertools
import random
import pandas as pd
import os

# -------------------------------
# Define the conditions
# -------------------------------
physical_velocity = [-1.5, -1.0, -0.5, 0, 0.5, 1.0, 1.5]
visual_multiplier   = [0.50, 0.75, 1.00, 1.25, 1.50]

num_participants = 15
repetitions = 5  # per condition

# Output base folder
output_base = 'Assets\\data\\study2\\p_sheets'
os.makedirs(output_base, exist_ok=True)

# -------------------------------
# Generate all physical × visual conditions
# -------------------------------
conditions = list(itertools.product(physical_velocity, visual_multiplier))

# -------------------------------
# Generate trials for one participant
# -------------------------------
def generate_participant_trials(pid):
    trials = []

    # Repeat each condition
    repeated_conditions = conditions * repetitions

    # Counterbalance by shuffling order per participant
    random.shuffle(repeated_conditions)

    for trial_num, (phys, vis) in enumerate(repeated_conditions):
        trial = {
            'pid': pid,
            'trial': trial_num,
            'physicalVelocity': phys,
            'visualMultiplier': vis,
            'congruent': None
        }
        trials.append(trial)

    return pd.DataFrame(trials)

# -------------------------------
# Save participant CSV
# -------------------------------
def save_participant_csv(pid, df, base_folder):
    participant_folder = os.path.join(base_folder, f'p{pid}')
    os.makedirs(participant_folder, exist_ok=True)

    csv_path = os.path.join(participant_folder, f'p{pid}_conditions.csv')
    df.to_csv(csv_path, index=False)
    print(f"Saved {csv_path}")

# -------------------------------
# Main loop
# -------------------------------
for pid in range(15, num_participants + 2):
    df = generate_participant_trials(pid)
    save_participant_csv(pid, df, output_base)
