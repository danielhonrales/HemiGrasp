// HemiGrasp PID control

#include <PID_v1.h>

////////////////////////////////////////////////////
// Config
////////////////////////////////////////////////////

// 1 = right hand, 0 = left hand
bool right_hand = 0;

// Pin variables
int ENA_M, IN1_M, IN2_M;
int ENA_T, IN1_T, IN2_T;
int ENB_L, IN3_L, IN4_L;
int encoderTA, encoderTB;
int encoderLA, encoderLB;
int encoderMA, encoderMB;

// Max encoder values (100%)
int maxMPos = 1000;
int maxTPos = 1000;
int maxLPos = 1000;

// Encoder position inputs
double mInput = 0;
double tInput = 0;
double lInput = 0;

// Motor speed outputs (converted to PWM)
double mOutput = 0;
double tOutput = 0;
double lOutput = 0;

// Encoder position setpoints
double mSetpoint = 0;
double tSetpoint = 0;
double lSetpoint = 0;

// PID constants
double kP = 0.7;
double kI = 0.0;
double kD = 0.0;

// Other controls
double minOutput = 5;
double minPWM = 40;

// PID controllers
PID mPID(&mInput, &mOutput, &mSetpoint, kP, kI, kD, DIRECT);
PID tPID(&tInput, &tOutput, &tSetpoint, kP, kI, kD, DIRECT);
PID lPID(&lInput, &lOutput, &lSetpoint, kP, kI, kD, DIRECT);

// Serial input command variable
String serial_input = "";

////////////////////////////////////////////////////
// Help Message
////////////////////////////////////////////////////

void print_help() {
  Serial.println("Commands:");
  Serial.println(" - Move motor(s): '[M/T/L/A],POSITION'   (Middle/Thumb/Little/All), 0-100%");
  Serial.println(" - Disable PID:   'STOP'");
  Serial.println(" - Enable PID:    'START'");
  Serial.println(" - Get positions: 'POS'");
  Serial.println(" - Set PID const: 'PID,kP,kI,kD'");
}

////////////////////////////////////////////////////
// Setup
////////////////////////////////////////////////////

void setup() {
  // Set pins
  if (right_hand) {
    ENA_M = 5;
    IN1_M = 6;
    IN2_M = 7;
    ENA_T = 8;
    IN1_T = 10; //9;
    IN2_T = 9; //10;
    ENB_L = 11;
    IN3_L = 12;
    IN4_L = 13;
    encoderTA = 19;
    encoderTB = 18;
    encoderLA = 21;
    encoderLB = 20;
    encoderMA = 3;
    encoderMB = 2;
  } else {
    ENA_M = 5;
    IN1_M = 7; //6;
    IN2_M = 6; //7;
    ENA_T = 8;
    IN1_T = 9; //10;
    IN2_T = 10; //9;
    ENB_L = 11;
    IN3_L = 13; //12;
    IN4_L = 12; //13;
    encoderTA = 19;
    encoderTB = 18;
    encoderLA = 20; //21;
    encoderLB = 21; //20;
    encoderMA = 3; //2;
    encoderMB = 2; //3;
  }

  // Start serial communication
  Serial.begin(115200); 
  delay(80); 
  
  // if (right_hand) {
  //   Serial.print("Right");
  // } else {
  //   Serial.print("Left");
  // }

  // Serial.println(" HemiGrasp device ready!");
  // print_help();

  // Setup pins
  pinMode(encoderMA, INPUT); 
  pinMode(encoderMB, INPUT); 
  pinMode(encoderTA, INPUT); 
  pinMode(encoderTB, INPUT); 
  pinMode(encoderLA, INPUT);
  pinMode(encoderLB, INPUT);
  pinMode(ENA_M, OUTPUT);
  pinMode(IN1_M, OUTPUT);
  pinMode(IN2_M, OUTPUT);
  pinMode(ENA_T, OUTPUT);
  pinMode(IN1_T, OUTPUT);
  pinMode(IN2_T, OUTPUT);
  pinMode(ENB_L, OUTPUT);
  pinMode(IN3_L, OUTPUT);
  pinMode(IN4_L, OUTPUT);
  attachInterrupt(digitalPinToInterrupt(encoderMA), ISR_encoderM, CHANGE);
  attachInterrupt(digitalPinToInterrupt(encoderTA), ISR_encoderT, CHANGE);
  attachInterrupt(digitalPinToInterrupt(encoderLA), ISR_encoderL, CHANGE);

  // Turn PID controllers on
  mPID.SetMode(AUTOMATIC);
  tPID.SetMode(AUTOMATIC);
  lPID.SetMode(AUTOMATIC);

  // Set PID output limits
  mPID.SetOutputLimits(-255, 255);
  tPID.SetOutputLimits(-255, 255);
  lPID.SetOutputLimits(-255, 255);
}

////////////////////////////////////////////////////
// Encoder Interrupts
////////////////////////////////////////////////////

void ISR_encoderM() {
  bool A = digitalRead(encoderMA);
  bool B = digitalRead(encoderMB);
  if (A == B) mInput++;
  else mInput--;
}

void ISR_encoderT() {
  bool A = digitalRead(encoderTA);
  bool B = digitalRead(encoderTB);
  if (A == B) tInput++;
  else tInput--;
}

void ISR_encoderL() {
  bool A = digitalRead(encoderLA);
  bool B = digitalRead(encoderLB);
  if (A == B) lInput++;
  else lInput--;
}

////////////////////////////////////////////////////
// Main Control Loop
////////////////////////////////////////////////////


void loop() {
  handle_serial_input();

  mPID.Compute();

  if (abs(mOutput) > minOutput) {
    setMotorM((int)mOutput);
  } else {
    setMotorM(0);
  }

  tPID.Compute();

  if (abs(tOutput) > minOutput) {
    setMotorT((int)tOutput);
  } else {
    setMotorT(0);
  }

  lPID.Compute();

  if (abs(lOutput) > minOutput) {
    setMotorL((int)lOutput);
  } else {
    setMotorL(0);
  }
}

////////////////////////////////////////////////////
// Manual Motor Control
////////////////////////////////////////////////////

void setMotorM(int speed) {
  if (speed > 0) { 
    digitalWrite(IN1_M, HIGH);
    digitalWrite(IN2_M, LOW);
    analogWrite(ENA_M, speed);
  } else if (speed < 0) {
    digitalWrite(IN1_M, LOW);
    digitalWrite(IN2_M, HIGH);
    analogWrite(ENA_M, abs(speed));
  } else {
    digitalWrite(IN1_M, LOW);
    digitalWrite(IN2_M, LOW);
    analogWrite(ENA_M, 0);
  }
}

void setMotorT(int speed) {
  if (speed > 0) {
    digitalWrite(IN1_T, HIGH);
    digitalWrite(IN2_T, LOW);
    analogWrite(ENA_T, speed);
  } else if (speed < 0) {
    digitalWrite(IN1_T, LOW);
    digitalWrite(IN2_T, HIGH);
    analogWrite(ENA_T, abs(speed));
  } else {
    digitalWrite(IN1_T, LOW);
    digitalWrite(IN2_T, LOW);
    analogWrite(ENA_T, 0);
  }
}

void setMotorL(int speed) {
  if (speed > 0) {
    digitalWrite(IN3_L, HIGH);
    digitalWrite(IN4_L, LOW);
    analogWrite(ENB_L, speed);
  } else if (speed < 0) {
    digitalWrite(IN3_L, LOW);
    digitalWrite(IN4_L, HIGH);
    analogWrite(ENB_L, abs(speed));
  } else {
    digitalWrite(IN3_L, LOW);
    digitalWrite(IN4_L, LOW);
    analogWrite(ENB_L, 0);
  }
}

void stopMotors() {
  setMotorM(0);
  setMotorT(0);
  setMotorL(0);
}

////////////////////////////////////////////////////
// Serial Parsing Functions
////////////////////////////////////////////////////

void handle_serial_input() {
  while (Serial.available()) {
    char inChar = (char)Serial.read();

    // Check for command termination
    if (inChar == '\n' || inChar == '\r') {
      serial_input.trim();

      if (serial_input.length() > 0) {
        // Serial.print("Received: '");
        // Serial.print(serial_input);
        // Serial.println("'");

        // Parse command
        if (serial_input.startsWith("M,")) {
          parse_m_command(serial_input.substring(2));
        } else if (serial_input.startsWith("T,")) {
          parse_t_command(serial_input.substring(2));
        } else if (serial_input.startsWith("L,")) {
          parse_l_command(serial_input.substring(2));
        } else if (serial_input.startsWith("A,")) {
          parse_a_command(serial_input.substring(2));
        } else if (serial_input.startsWith("PID,")) {
          parse_pid_command(serial_input.substring(4));
        } else if (serial_input.startsWith("POS")) {
          // Serial.print("Encoder positions: M ");
          // Serial.print(mInput);
          // Serial.print(" | T ");
          // Serial.print(tInput);
          // Serial.print(" | L ");
          // Serial.println(lInput);

          Serial.print(mInput);
          Serial.print(",");
          Serial.print(tInput);
          Serial.print(",");
          Serial.print(lInput);
        } else if (serial_input.equalsIgnoreCase("STOP")) {
          mPID.SetMode(MANUAL);
          tPID.SetMode(MANUAL);
          lPID.SetMode(MANUAL);
          mOutput = 0;
          tOutput = 0;
          lOutput = 0;
          stopMotors();
          // Serial.println("PID control disabled.");
        } else if (serial_input.equalsIgnoreCase("START")) {
          mPID.SetMode(AUTOMATIC);
          tPID.SetMode(AUTOMATIC);
          lPID.SetMode(AUTOMATIC);
          // Serial.println("PID control enabled.");
        } else {
          // Serial.println("Invalid command.");
          print_help();
        }
      }

      serial_input = "";
    } else {
      serial_input += inChar;
    }
  }
}

// Reusable token parser
float get_next_token(String &data, int &start_index) {
  int end_index = data.indexOf(',', start_index);
  String token;

  if (end_index == -1) {
    token = data.substring(start_index);
    start_index = data.length();;
  } else {
    token = data.substring(start_index, end_index);
    start_index = end_index + 1;
  }

  token.trim();
  return token.toFloat();
}

void parse_m_command(String data) {
  int parse_index = 0;

  long position = (long)get_next_token(data, parse_index);
  position = constrain(position, 0, 100);

  // Serial.print("Moving motor M to position ");
  // Serial.println(position);

  mSetpoint = map(position, 0, 100, 0, maxMPos);
}

void parse_t_command(String data) {
  int parse_index = 0;

  long position = (long)get_next_token(data, parse_index);
  position = constrain(position, 0, 100);

  // Serial.print("Moving motor T to position ");
  // Serial.println(position);

  tSetpoint = map(position, 0, 100, 0, maxTPos);
}

void parse_l_command(String data) {
  int parse_index = 0;

  long position = (long)get_next_token(data, parse_index);
  position = constrain(position, 0, 100);

  // Serial.print("Moving motor L to position ");
  // Serial.println(position);

  lSetpoint = map(position, 0, 100, 0, maxLPos);
}

void parse_a_command(String data) {
  int parse_index = 0;

  long position = (long)get_next_token(data, parse_index);
  position = constrain(position, 0, 100);

  // Serial.print("Moving all motors to position ");
  // Serial.println(position);

  mSetpoint = map(position, 0, 100, 0, maxMPos);
  tSetpoint = map(position, 0, 100, 0, maxTPos);
  lSetpoint = map(position, 0, 100, 0, maxLPos);
}

void parse_pid_command(String data) {
  int parse_index = 0;

  kP = get_next_token(data, parse_index);
  kI = get_next_token(data, parse_index);
  kD = get_next_token(data, parse_index);

  Serial.print("Set kP to ");
  Serial.print(kP);
  Serial.print(", kI to ");
  Serial.print(kI);
  Serial.print(", and kD to ");
  Serial.print(kD);
  Serial.println(".");
}