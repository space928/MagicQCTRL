/*
 * Configuration file storing compile time defaults and config variables
 */

#ifndef CONFIG_H
#define CONFIG_H


// ===================================
// CONSTS
#define DISPLAY_TIMEOUT 1000 // Display timeout in ms
#define N_PAGES 3 // How many pages of keys are allowed
#define ENABLE_DEBUG_PAGE 1 // Set to 1 to enable; 0 to disable
#define DEFAULT_BRIGHTNESS 40 // Default neopixel brightness

///////////////////////////////////////
//// DEBUGGING CONSTS
// Serial Logging
#define DEBUG_SERIAL_LOGGING

// Uncomment whichever level of logging you want
#define DEBUG_VERBOSE
// #define DEBUG_INFO
// #define DEBUG_WARNING
// #define DEBUG_FATAL

// Display
#ifdef PLATFORM_HELTEC_WIFIKIT_32
#define DEBUG_DISPLAY_ENABLE
#define DEBUG_DISPLAY_AUDIO
#define DEBUG_DISPLAY_FFT

#define DEBUG_DISPLAY_WIDTH 128
#define DEBUG_DISPLAY_HEIGHT 64
#define DEBUG_SCREEN_ADDRESS 0x3c
#define DEBUG_DISPLAY_HARDWARE_PARAMS DEBUG_DISPLAY_WIDTH, DEBUG_DISPLAY_HEIGHT, &Wire, RST_OLED

#define CPU_FREQ_KHZ 240000
#endif

#endif //CONFIG_H
