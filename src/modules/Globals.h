/*
 * Project wide globals
 */

#ifndef GLOBALS_H
#define GLOBALS_H

#include <RotaryEncoder.h>
#include <Adafruit_SH110X.h>
#include <Adafruit_NeoPixel.h>
//#include <EasyPCF8575.h>


// ===================================
// GLOBALS
// Create the rotary encoder
extern RotaryEncoder encoder;

// our encoder position state
extern int encoder_pos;
extern bool switch_states[12];

extern Adafruit_SH1106G display;
extern int display_page;

extern Adafruit_NeoPixel pixels;

#endif //GLOBALS_H
