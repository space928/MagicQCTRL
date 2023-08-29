#include <RotaryEncoder.h>
#include <Wire.h>
#include "Config.h"
#include "debug.h"
#include "Display.h"
#include "Globals.h"
#include "HID.h"

// ===================================
// GLOBALS
// Create the rotary encoder
RotaryEncoder encoder(PIN_ROTB, PIN_ROTA, RotaryEncoder::LatchMode::FOUR3);

// our encoder position state
int encoder_pos = 0;
bool encoder_pressed = false;
bool switch_states[N_KEYS] = {false};
uint32_t key_colours_low[N_KEYS * N_PAGES] = {0x4A3520};
uint32_t key_colours_high[N_KEYS * N_PAGES] = {0xAAAAAA};
int8_t encoder_states[N_ENCODERS] = {0};
int8_t encoder_dirs[N_ENCODERS] = {0};
uint8_t encoder_switches[N_ENCODERS] = {0};
uint32_t last_encoder_msg_time = 0;

// Rotary encoder state machine from: http://www.mathertel.de/Arduino/RotaryEncoderLibrary.aspx
// The array holds the values -1 for the entries where a position was 
// decremented, a 1 for the entries where the position was incremented
// and 0 in all the other (no change or not valid) cases.
const int8_t KNOB_DIR[] = {
    0, -1, 1, 0,
    1, 0, 0, -1,
    -1, 0, 0, 1,
    0, 1, -1, 0
};

// ===================================
// METHODS

void checkPosition() {
    encoder.tick();  // just call tick() to check the state.
}

void checkEncoders() {
    // Read the vale of the IO expanders
    //dbg_log("INT=%d\n", digitalRead(PIN_SPEAKER));
    if(digitalRead(PIN_SPEAKER) != LOW)
        return;

    uint8_t pcfData[4];
    Wire.requestFrom(PCF_L_ADDR, 2);
    if (Wire.available()){
        Wire.readBytes(pcfData, 2);
    }
    Wire.requestFrom(PCF_R_ADDR, 2);
    if (Wire.available()){
        Wire.readBytes(pcfData + 2, 2);
    }

    uint32_t encoderData = pcfData[0] | (pcfData[1] << 8) 
                           | (pcfData[2] << 16) | (pcfData[3] << 24);

    //dbg_log("Encoder data: " BYTE_TO_BINARY_PATTERN " " BYTE_TO_BINARY_PATTERN " " BYTE_TO_BINARY_PATTERN " " BYTE_TO_BINARY_PATTERN "\n", BYTE_TO_BINARY(pcfData[3]), BYTE_TO_BINARY(pcfData[2]), BYTE_TO_BINARY(pcfData[1]), BYTE_TO_BINARY(pcfData[0]));

    // Each encoder uses 3 pins on the PCF:
    //  0 - E1_RotA
    //  1 - E1_RotB
    //  2 - E1_Switch
    //  3 - E2_RotA
    //  4 - E2_RotB
    //  5 - E2_Switch
    //  ...

    // This algorithm is derived from: 
    // http://www.mathertel.de/Arduino/RotaryEncoderLibrary.aspx
    for(int i = 0; i < N_ENCODERS; i++) {
        int a = (i%4)*2 + (i>=4?16:0);
        int rotA = (encoderData >> a) & 1;
        int rotB = (encoderData >> (a+1)) & 1;
        // Set the 1 bit of the encoder_switches item to the value of the switch pin
        uint8_t switchVal = (~(encoderData >> (i+(i>=4?16:0)+8)) & 1);
        encoder_switches[i] ^= (-switchVal ^ encoder_switches[i]) & 1;
        int8_t currState = rotA | (rotB << 1);

        int8_t lastState = encoder_states[i];
        if (lastState != currState) {
            encoder_dirs[i] += KNOB_DIR[currState | (lastState << 2)];
            encoder_states[i] = currState;
            //dbg_log("Encoder msg! a=%d b=%d s=%d d=%d a=%d i=%d\n", rotA, rotB, currState, encoder_dirs[i], a, i);
        } else {
            //dbg_log("Encoder! a=%d b=%d s=%d d=%d a=%d i=%d\n", rotA, rotB, currState, encoder_dirs[i], a, i);
        }
    }
}

// Input a value 0 to 255 to get a color value.
// The colours are a transition r - g - b - back to r.
uint32_t hue2RGB(byte wheelPos) {
    if (wheelPos < 85) {
        return pixels.Color(255 - wheelPos * 3, 0, wheelPos * 3);
    } else if (wheelPos < 170) {
        wheelPos -= 85;
        return pixels.Color(0, wheelPos * 3, 255 - wheelPos * 3);
    } else {
        wheelPos -= 170;
        return pixels.Color(wheelPos * 3, 255 - wheelPos * 3, 0);
    }
}

void setupIO() {
    // set rotary encoder inputs and interrupts
    pinMode(PIN_ROTA, INPUT_PULLUP);
    pinMode(PIN_ROTB, INPUT_PULLUP);
    attachInterrupt(digitalPinToInterrupt(PIN_ROTA), checkPosition, PinStatus::CHANGE);
    attachInterrupt(digitalPinToInterrupt(PIN_ROTB), checkPosition, PinStatus::CHANGE);

    // We will use I2C for scanning the Stemma QT port
    Wire.begin();

    // set all mechanical keys to inputs
    for (uint8_t i = 0; i <= 12; i++) {
        pinMode(i, INPUT_PULLUP);
    }

    // Disable speaker
    pinMode(PIN_SPEAKER_ENABLE, OUTPUT);
    digitalWrite(PIN_SPEAKER_ENABLE, LOW);
    // Use the speaker pin as an interrupt
    pinMode(PIN_SPEAKER, INPUT_PULLUP);

    // Setup IO expanders
    //Wire.setSCL(PIN_WIRE0_SCL);
    //Wire.setSDA(PIN_WIRE0_SDA);
    Wire.begin();

    // Test the connection with the IO expanders
    Wire.beginTransmission(PCF_L_ADDR);
    uint8_t i2cError = Wire.endTransmission();
    Wire.beginTransmission(PCF_R_ADDR);
    i2cError |= Wire.endTransmission();
    if(i2cError != 0) {
        dbg_log_warning("Failed to connect to rotary encoders! Check I2C address and wiring.");
        display.setCursor(0, 8);
        display.println("Failed to connect to rotary encoders!");
        display.display();
        delay(1000);
    } else {
        // Only enable the encoder interrupt if the IO expanders were detected
        attachInterrupt(digitalPinToInterrupt(PIN_SPEAKER), checkEncoders, PinStatus::FALLING);
    }
}

void setKeyCol(uint8_t keyId, uint32_t colLow, uint32_t colHigh) {
    key_colours_low[keyId] = colLow;
    key_colours_high[keyId] = colHigh;
}

void tickIO() {
    // Read the encoder
    encoder.tick();          
    int newPos = encoder.getPosition();
    if (encoder_pos != newPos) {
        changePage((int)encoder.getDirection());
        encoder_pos = newPos;
        wakeDisplay();
    }

    // Check encoder press
    encoder_pressed = false;
    if (!digitalRead(PIN_SWITCH)) {
        encoder_pressed = true;
        wakeDisplay();
    }
    
    // Read keyswitches
    for (int i = 1; i <= N_KEYS; i++) {
        if (!digitalRead(i)) { // switch pressed!
            if (display_page == -1 || display_page == N_PAGES)
                pixels.setPixelColor(i - 1, hue2RGB(((i * 256 / N_KEYS) + millis()/8) & 255)); // make colourful
            else
                pixels.setPixelColor(i - 1, key_colours_high[i-1 + display_page * N_KEYS]);

            wakeDisplay();

            if(!switch_states[i-1])
                sendKey(display_page, i-1, true);
            
            switch_states[i-1] = true;
        } else {
            if(switch_states[i-1])
                sendKey(display_page, i-1, false);
            
            if (display_page == -1 || display_page == N_PAGES)
                pixels.setPixelColor(i - 1, 0x110f0a);
            else
                pixels.setPixelColor(i - 1, key_colours_low[i-1 + display_page * N_KEYS]);

            switch_states[i-1] = false;
        }
    }

    // Check encoders
    checkEncoders();
    if(millis() - last_encoder_msg_time >= (1000 / ENCODER_MSG_RATE)) {
        for (uint8_t i = 0; i < N_ENCODERS; i++) {
            if (encoder_dirs[i] != 0) {
                float dir = pow(abs(encoder_dirs[i]), ENCODER_ACCELERATION);
                dir = copysign(dir, encoder_dirs[i]);
                sendEncoder(display_page, i, (int8_t)dir);
                encoder_dirs[i] = 0;
                last_encoder_msg_time = millis();
            }
        }
    }

    for(uint8_t i = 0; i < N_ENCODERS; i++) {
        // encoder_switches stores the current value of the switch in the 1 bit 
        // and the previous value in the 2 bit
        uint8_t switchVal = encoder_switches[i];
        if (switchVal != 0 || switchVal != 3) {
            // Copy the current value bit to the previous value bit
            encoder_switches[i] = switchVal | (switchVal << 1);
            //sendButton(display_page, i, (switchVal & 1) == 1);
        }
    }
}
