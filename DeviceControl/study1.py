import serial
import time
import random

# Config
PORT = "COM6"
BAUD = 115200

error_warning = 50 # Error threshold to warn

# Setup
arduino = serial.Serial(PORT, BAUD, timeout=0.01)
active = "A"

time.sleep(2)

def send_cmd(command):
    arduino.write(f"{command}\n".encode())

def get_pos_and_error(location):
    line = arduino.readline().decode("utf-8").strip()
    values = line.split(',')

    m = float(values[0])
    t = float(values[1])
    l = float(values[2])

    m_err = abs(m - (float(location) * 10))
    t_err = abs(t - (float(location) * 10))
    l_err = abs(l - (float(location) * 10))

    return m, t, l, m_err, t_err, l_err

def go_to(location, calibration=False):
    send_cmd("START")

    # For harder detection
    if (not calibration):
        randA = random.randrange(101)
        randB = random.randrange(101)

        send_cmd(f"{active},{randA}")
        time.sleep(0.25)

        send_cmd(f"{active},{randB}")
        time.sleep(0.25)

        print(f"Rand pos:  A {(randA * 10):4.0f} | B {(randB * 10):4.0f}")

    send_cmd(f"{active},{location}")
    time.sleep(1)
    send_cmd("STOP")
    send_cmd("POS")

    m, t, l, m_err, t_err, l_err = get_pos_and_error(location)

    if (m_err >= error_warning or t_err >= error_warning or l_err >= error_warning):
        warning = "!!!"
    else:
        warning = ""

    print(f"Positions: M {m:4.0f} | T {t:4.0f} | L {l:4.0f}")
    print(f"Error:     M {m_err:4.0f} | T {t_err:4.0f} | L {l_err:4.0f} {warning}")

while (True):
    command = input(f"=== Active motor(s): {active} ===\nCommands:\n - 'ddd': Go to desired volume (ddd%)\n - 'home': Go to home position (0%)\n - 'full': Go to full position (100%)\n - '[a/m/t/l]': Select active motor(s):\n> ") 

    if (command.isdigit() and 0 <= int(command) <= 100):
        go_to(int(command))
        # match command:
        #     case "0":
        #         go_to(0)
        #     case "25":
        #         go_to(25)
        #     case "50":
        #         go_to(45)
        #     case "75":
        #         go_to(64)
        #     case "100":
        #         go_to(82)
        #     case _:
        #         print("Invalid command.")
    elif (command.lower() == "home"):
        go_to(0, True)
    elif (command.lower() == "full"):
        go_to(100, True)
    elif (command.lower() == "a"):
        active = "A"
    elif (command.lower() == "m"):
        active = "M"
    elif (command.lower() == "t"):
        active = "T"
    elif (command.lower() == "l"):
        active = "L"
    else:
        print("Invalid command.")