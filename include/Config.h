/*
 * Configuration file storing compile time defaults and config variables
 */

#ifndef CONFIG_H
#define CONFIG_H


// ===================================
// CONSTS
#define DISPLAY_TIMEOUT 1000 // Display timeout in ms
#define N_PAGES 3 // How many pages of keys are allowed
#define N_KEYS 12
#define N_ENCODERS 8
#define ENABLE_DEBUG_PAGE 0 // Set to 1 to enable; 0 to disable
#define DEFAULT_BRIGHTNESS 128 // Default neopixel brightness 0-255
#define KEY_NAME_LENGTH 6
#define DISPLAY_WIDTH 128
#define DISPLAY_HEIGHT 64
#define VERSION_STRING "1.6"
#define PCF_L_ADDR 0x20
#define PCF_R_ADDR 0x21
#define ENCODER_MSG_RATE 40
#define ENCODER_ACCELERATION 1.7

///////////////////////////////////////
//// DEBUGGING CONSTS
// Serial Logging
#define DEBUG_SERIAL_LOGGING

// Uncomment whichever level of logging you want
// #define DEBUG_VERBOSE
#define DEBUG_INFO
// #define DEBUG_WARNING
// #define DEBUG_FATAL

#endif //CONFIG_H
