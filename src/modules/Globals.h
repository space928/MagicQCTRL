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
extern bool encoder_pressed;
extern bool switch_states[N_KEYS];
extern int8_t encoder_dirs[N_ENCODERS];
extern uint8_t encoder_switches[N_ENCODERS];

extern Adafruit_SH1106G display;
extern int8_t display_page;

extern Adafruit_NeoPixel pixels;

#endif //GLOBALS_H
