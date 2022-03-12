/**
 * Arduino UNO PLC for Shapeoko CNC Router
 * Shield: Rigged Circuits 24V Industrial I/O - https://www.rugged-circuits.com/24v-industrial/24v-industrial-shield / https://www.rugged-circuits.com/24v-industrial-tech-page
 * Roles:
 * - ATC PNP sensor conditioning to UC300ETH-5LPT/UCCNC
 *     The ATC PNP sensor is outputing at a lower voltage (Vout: ~5V) than supplied (Vcc: 24V).
 *     This causes UC300ETH/UB1 to not detect the state change on the input pins selected (requires 24V source).
 *
 * - E-STOP state output to LED
 *    Depending on the state of power and e-stop, will blink LED at different delays or steady if CNC is on and ready
 */

#include <Wire.h>
#include <Adafruit_ADS1X15.h> // v2.3.0
#include <Adafruit_MCP23017.h> // v1.3.0 (bug in 2.0.2, https://github.com/adafruit/Adafruit-MCP23017-Arduino-Library/issues/77)
#include <elapsedMillis.h> // v1.0.6

Adafruit_ADS1015 ads1;
Adafruit_MCP23017 mcp1;

uint8_t OUTPUT_RESETLED = 0;
uint8_t OUTPUT_TOOLCLAMP = 1;
uint8_t OUTPUT_TOOLRELEASE = 2;

uint8_t INPUT_PWR = 8;
uint8_t INPUT_ESTOP = 9;
uint8_t INPUT_CONTACTOR = 10;

uint8_t INPUT_TOOLCLAMP = 12;
uint8_t INPUT_TOOLRELEASE = 13;

bool TOOLRELEASE = false;
bool TOOLCLAMP = false;

uint16_t STATE_LAST = 0;

bool POWER_ON = false;
bool ESTOP_CLEAR = false;
bool CONTACTOR_CLOSED = false;

elapsedMillis timeElapsed;
unsigned int INTERVAL_ESTOP = 1000;
unsigned int INTERVAL_CONTACTOR = 500;

void setup() {
  Serial.begin(38400);
  Serial.println("");
  Serial.println("BOOT UP");
  
  Serial.println("SETUP PLC");
  ads1.setGain(GAIN_ONE);
  ads1.begin(72); // (72); /* Use this for the 12-bit version, 73 if the jumper is shorted) */
  mcp1.begin(1);
  
  mcp1.pinMode(OUTPUT_RESETLED, OUTPUT);
  mcp1.digitalWrite(OUTPUT_RESETLED, LOW);

  mcp1.pinMode(OUTPUT_TOOLCLAMP, OUTPUT);
  mcp1.digitalWrite(OUTPUT_TOOLCLAMP, LOW);
  mcp1.pinMode(OUTPUT_TOOLRELEASE, OUTPUT);
  mcp1.digitalWrite(OUTPUT_TOOLRELEASE, LOW);
  
  mcp1.pinMode(INPUT_PWR, INPUT);
  mcp1.pinMode(INPUT_ESTOP, INPUT);
  mcp1.pinMode(INPUT_CONTACTOR, INPUT);

  mcp1.pinMode(INPUT_TOOLCLAMP, INPUT);
  mcp1.pinMode(INPUT_TOOLRELEASE, INPUT);

  Serial.println("STARTED");
}


void loop() {
  uint16_t state_current = mcp1.readGPIOAB();

  // Get current states (LOW )
  POWER_ON = getState(state_current, INPUT_PWR) == LOW;
  ESTOP_CLEAR = getState(state_current, INPUT_ESTOP) == LOW;
  CONTACTOR_CLOSED = getState(state_current, INPUT_CONTACTOR) == LOW;

  // Condition ATC sensors to CNC controller
  TOOLCLAMP = getState(state_current, INPUT_TOOLCLAMP) == LOW;
  TOOLRELEASE = getState(state_current, INPUT_TOOLRELEASE) == LOW;
  setOutput(OUTPUT_TOOLCLAMP, TOOLCLAMP ? HIGH : LOW);
  setOutput(OUTPUT_TOOLRELEASE, TOOLRELEASE ? HIGH : LOW);
  
  // If only power, flash led signaling ESTOP
  if(POWER_ON && !ESTOP_CLEAR && !CONTACTOR_CLOSED) {
    if(timeElapsed > INTERVAL_ESTOP){
      toggleOutput(state_current, OUTPUT_RESETLED);
      timeElapsed = 0;
    }
  }

  // If power and estop, flash led signaling RESET (contactor)
  if(POWER_ON && ESTOP_CLEAR && !CONTACTOR_CLOSED) {
    if(timeElapsed > INTERVAL_CONTACTOR){
      toggleOutput(state_current, OUTPUT_RESETLED);
      timeElapsed = 0;
    }
  }

  // If power, estop and reset (contactor) then led on
  if(POWER_ON && ESTOP_CLEAR && CONTACTOR_CLOSED) {
    if(getState(state_current, OUTPUT_RESETLED) == LOW) {
      setOutput(OUTPUT_RESETLED, HIGH);
    }
  }

  // Serial Print if inputs changed
  if(state_current >> 8 != STATE_LAST){
    STATE_LAST = state_current >> 8;
    printStateChange(state_current);
  }
}

// Toggle an output state
void toggleOutput(uint16_t state, uint8_t pin) {
    uint8_t value = getState(state, pin) ? LOW : HIGH;
    mcp1.digitalWrite(pin, value);
}

// Set an output state
void setOutput(uint8_t pin, uint8_t value) {
    mcp1.digitalWrite(pin, value);
}

// Get state on a pin
uint8_t getState(uint16_t state, uint8_t pin) {
  uint16_t pin_mask = 1 << pin;
  return (state & pin_mask) == pin_mask ? HIGH : LOW;
}

// Print debug information
void printStateChange(uint16_t state) {
  Serial.print("STATE: "); Serial.println(state, BIN);
  Serial.print("POWER: "); Serial.print(POWER_ON);
  Serial.print("; ESTOP: "); Serial.print(ESTOP_CLEAR);
  Serial.print("; CONTACTOR: "); Serial.println(CONTACTOR_CLOSED);
  
  Serial.print("TOOL CLAMP: "); Serial.print(TOOLRELEASE);
  Serial.print("; RELEASE: "); Serial.println(TOOLCLAMP);
  
  Serial.println(" ");
}
