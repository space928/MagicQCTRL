#include <Arduino.h>
// #include <pins_arduino.h>
#include <Adafruit_SH110X.h>
#include <Adafruit_NeoPixel.h>
#include <RotaryEncoder.h>
#include <Wire.h>
#include "Config.h"
#include "debug.h"
#include "Globals.h"
#include "Display.h"
#include "HID.h"
#include "EncoderKeysIO.h"

// ===================================
// GLOBALS
// Create the neopixel strip with the built in definitions NUM_NEOPIXEL and PIN_NEOPIXEL
Adafruit_NeoPixel pixels = Adafruit_NeoPixel(NUM_NEOPIXEL, PIN_NEOPIXEL, NEO_GRB + NEO_KHZ800);

// ===================================
// LOCALS
uint8_t j = 0;

// ===================================
// METHODS
void setup()
{
    setupDebug();
    // while (!Serial) { delay(10); }     // wait till serial port is opened
    delay(100); // RP2040 delay is not a bad idea

    dbg_log_info("MagicQ CTRL V" VERSION_STRING " Init");

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

    display.setCursor(0, 16);
    display.println("Initialised!");
    display.display();

    // Startup animation <3
    startupAnim();

    dbg_log_info("Initialisation completed!");
}

void loop()
{
    tickIO();
    pixels.show();
    updateDisplay();

    j++;
}
