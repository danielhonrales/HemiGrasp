import itertools
import pandas as pd
import os

# -------------------------------
# Define the conditions
# -------------------------------
technique = ["oneHand", "twoHand"]
fixed_factor = ["fixedVolume", "fixedVisual"]

num_participants = 8   # number of participants
num_trials_per_sheet = 300  # trials per condition/sheet

# Output base folder
output_base = 'Assets\\data\\p_sheets'
os.makedirs(output_base, exist_ok=True)

# -------------------------------
# Generate all combinations of technique × fixed_factor
# -------------------------------
def generate_combinations():
    return list(itertools.product(technique, fixed_factor))

# -------------------------------
# Generate trial DataFrames for one participant
# -------------------------------
def generate_participant_sheets(pid, num_trials):
    sheets = {}
    for combo in generate_combinations():
        tech, fixed = combo
        sheet_name = f"p{pid}_{tech}_{fixed}"

        trials = []

        for trial_num in range(0, num_trials):
            staircase_label = "A" if trial_num % 2 == 0 else "B"
            trial = {
                'pid': pid,
                'trial': trial_num,
                'staircase': staircase_label,
                'volumeSize': None,
                'visualSize': None,
                'response': None,
                'reversal': None,
                'step': None,
            }

            # Fill volumeSize / visualSize based on fixed factor
            if fixed == "fixedVolume":
                trial['volumeSize'] = 1
                # Counterbalance first two visualSize values
                if trial_num == 0:
                    trial['visualSize'] = 0 if pid % 2 != 0 else 2
                elif trial_num == 1:
                    trial['visualSize'] = 2 if pid % 2 != 0 else 0
            elif fixed == "fixedVisual":
                trial['visualSize'] = 1
                # Counterbalance first two volumeSize values
                if trial_num == 0:
                    trial['volumeSize'] = 0 if pid % 2 != 0 else 2
                elif trial_num == 1:
                    trial['volumeSize'] = 2 if pid % 2 != 0 else 0

            trials.append(trial)

        sheets[sheet_name] = pd.DataFrame(trials)

    return sheets

# -------------------------------
# Save each sheet as a CSV in the participant folder
# -------------------------------
def save_participant_csvs(pid, sheets, base_folder):
    # Create a folder for this participant
    participant_folder = os.path.join(base_folder, f'p{pid}')
    os.makedirs(participant_folder, exist_ok=True)

    for sheet_name, df in sheets.items():
        csv_path = os.path.join(participant_folder, f'{sheet_name}.csv')
        df.to_csv(csv_path, index=False)
        print(f"Saved {csv_path}")

# -------------------------------
# Main loop: generate & save for all participants
# -------------------------------
for pid in range(1, num_participants + 1):
    participant_sheets = generate_participant_sheets(pid, num_trials_per_sheet)
    save_participant_csvs(pid, participant_sheets, output_base)
