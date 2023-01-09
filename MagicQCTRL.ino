#include <Adafruit_SH110X.h>
#include <Adafruit_NeoPixel.h>
#include <RotaryEncoder.h>
#include <Wire.h>
#include "Config.h"
#include "Globals.h"
#include "Display.h"
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
void setup() {
  Serial.begin(115200);
  //while (!Serial) { delay(10); }     // wait till serial port is opened
  delay(100);  // RP2040 delay is not a bad idea

  Serial.println("MagicQ CTRL Init");

  // start pixels!
  pixels.begin();
  pixels.setBrightness(DEFAULT_BRIGHTNESS);
  pixels.show(); // Initialize all pixels to 'off'

  // Setup everything required for the OLED
  setupDisplay();

  // Setup everything required for the encoders and keys
  setupIO();
}

void loop() {
  for (int i = 0; i < pixels.numPixels(); i++) {
    pixels.setPixelColor(i, 0x4A3520);
  }
  
  // Read the state of all the keys and encoders
  tickIO();

  // show neopixels, increment swirl
  pixels.show();

  // Update the display
  updateDisplay();
  
  j++;
}
