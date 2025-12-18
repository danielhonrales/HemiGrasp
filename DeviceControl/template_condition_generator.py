import itertools
import random
import pandas as pd
import os

# Define the conditions
technique = ["onehand", "twohand"]
fixed_factor = ["visual", "physical"]
threshold = ["upper", "lower"]

# Create the output folder
output_folder = "participant_sheets"
os.makedirs(output_folder, exist_ok=True)

# Generate all possible combinations
def generate_combinations():
    return list(itertools.product(technique, fixed_factor, threshold))

# Generate participant orders
def generate_orders(num_participants, trials_per_condition=50):
    orders = []
    all_combinations = generate_combinations()

    for participant in range(num_participants):
        participant_orders = []

        # Randomize condition order per participant
        randomized_combinations = all_combinations.copy()
        random.shuffle(randomized_combinations)

        for technique, fixed_factor, threshold in randomized_combinations:
            # Expand each condition into trials
            for trial in range(1, trials_per_condition + 1):
                participant_orders.append({
                    "Participant": participant + 1,
                    "Technique": technique,
                    "FixedFactor": fixed_factor,
                    "Threshold": threshold,
                    "Trial": trial,
                    "RadiusChange": "",
                    "StepSize": "",
                    "Response": "",
                    "Reversal": ""
                })

        orders.append(participant_orders)

    return orders

# Save each participant's order
def save_to_files(orders):
    for participant_orders in orders:
        participant_number = participant_orders[0]["Participant"]
        df = pd.DataFrame(participant_orders)

        # Excel
        excel_path = os.path.join(
            output_folder, f"p{participant_number}.xlsx"
        )
        df.to_excel(excel_path, index=False)

        # CSV
        csv_path = os.path.join(
            output_folder, f"p{participant_number}.csv"
        )
        df.to_csv(csv_path, index=False)

        print(f"Saved p{participant_number}")

# Generate and save
num_participants = 12
orders = generate_orders(num_participants)
save_to_files(orders)
