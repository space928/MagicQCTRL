#include <Arduino.h>
//#include <pins_arduino.h>
#include <Adafruit_SH110X.h>
#include <Adafruit_NeoPixel.h>
#include <RotaryEncoder.h>
#include <Wire.h>
#include "src/modules/Config.h"
#include "src/modules/debug.h"
#include "src/modules/Globals.h"
#include "src/modules/Display.h"
#include "src/modules/HID.h"
#include "src/modules/EncoderKeysIO.h"


// ===================================
// GLOBALS
// Create the neopixel strip with the built in definitions NUM_NEOPIXEL and PIN_NEOPIXEL
Adafruit_NeoPixel pixels = Adafruit_NeoPixel(NUM_NEOPIXEL, PIN_NEOPIXEL, NEO_GRB + NEO_KHZ800);

// ===================================
// LOCALS
uint8_t j = 0;

// ===================================
// METHODS
void setup() {
    setupDebug();
    //while (!Serial) { delay(10); }     // wait till serial port is opened
    delay(100);  // RP2040 delay is not a bad idea

    dbg_log_info("MagicQ CTRL Init");

    // start pixels!
    pixels.begin();
    pixels.setBrightness(DEFAULT_BRIGHTNESS);
    pixels.show(); // Initialize all pixels to 'off'

    // Setup everything required for the OLED
    setupDisplay();

    // Setup everything required for the encoders and keys
    setupIO();

    // Setup the hid device
    initHID();

    for (int i = 0; i < pixels.numPixels(); i++) {
        pixels.setPixelColor(i, 0x4A3520);
    }

    dbg_log_info("Initialisation completed!");
}

void loop() {
    tickIO();
    pixels.show();
    updateDisplay();
    
    j++;
}
