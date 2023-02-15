//#include <EasyPCF8575.h>
#include <RotaryEncoder.h>
#include <Wire.h>
#include "Config.h"
#include "Display.h"
#include "Globals.h"
#include "HID.h"

// ===================================
// GLOBALS
// Create the rotary encoder
RotaryEncoder encoder(PIN_ROTB, PIN_ROTA, RotaryEncoder::LatchMode::FOUR3);

// our encoder position state
int encoder_pos = 0;
bool switch_states[12] = {false};

// ===================================
// METHODS

void checkPosition() {
    encoder.tick();  // just call tick() to check the state.
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
    attachInterrupt(digitalPinToInterrupt(PIN_ROTA), checkPosition, CHANGE);
    attachInterrupt(digitalPinToInterrupt(PIN_ROTB), checkPosition, CHANGE);

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
    pinMode(PIN_SPEAKER, INPUT);
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
    if (!digitalRead(PIN_SWITCH)) {
        wakeDisplay();
        Serial.println("Encoder button");
    }
    
    // Read keyswitches
    for (int i = 1; i <= 12; i++) {
        if (!digitalRead(i)) { // switch pressed!
            pixels.setPixelColor(i - 1, hue2RGB(((i * 256 / pixels.numPixels()) + millis()/8) & 255)); // make colourful
            wakeDisplay();

            if(!switch_states[i-1])
                sendKey(display_page, i-1, true);
            
            switch_states[i-1] = true;
        } else {
            if(switch_states[i-1])
                sendKey(display_page, i-1, false);
            
            switch_states[i-1] = false;
        }
    }
}
