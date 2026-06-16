import itertools
import random
import pandas as pd
import os

# -------------------------------
# Define the conditions
# -------------------------------
physical_speed = ["0", "10", "20", "40"]
visual_end_size = ["80", "100", "120", "140", "160", "180"]
scenario = ["one-hand", "two-hand"]

num_participants = 12
repetitions = 4  # per condition

num_trials_in_block = len(physical_speed) * len(visual_end_size) * repetitions

# Output base folder
output_base = 'p_sheets'
os.makedirs(output_base, exist_ok=True)

# -------------------------------
# Generate all physical × visual × rendering conditions
# -------------------------------
conditions = list(itertools.product(physical_speed, visual_end_size))

# -------------------------------
# Generate trials for one participant
# -------------------------------
def generate_participant_trials(pid):
    trials = []

    # Repeat each condition
    repeated_conditions = conditions * repetitions

    # Block 1
    if (pid % 2 == 0):
        scen = scenario[0]
    else:
        scen = scenario[1]

    # Counterbalance by shuffling order per participant
    random.shuffle(repeated_conditions)

    for trial_num, (phys, vis) in enumerate(repeated_conditions):
        trial = {
            'pid': pid,
            'trial': trial_num,
            'physical_speed': phys,
            'visual_end_size': vis,
            'scenario': scen,
            'congruency': None
        }
        trials.append(trial)
    
    # Block 2
    if (pid % 2 == 0):
        scen = scenario[1]
    else:
        scen = scenario[0]

    # Counterbalance by shuffling order per participant
    random.shuffle(repeated_conditions)

    for trial_num, (phys, vis) in enumerate(repeated_conditions):
        trial = {
            'pid': pid,
            'trial': trial_num + num_trials_in_block,
            'physical_speed': phys,
            'visual_end_size': vis,
            'scenario': scen,
            'congruency': None
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
for pid in range(1, num_participants + 1):
    df = generate_participant_trials(pid)
    save_participant_csv(pid, df, output_base)
