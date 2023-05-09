#ifndef DEBUG_H
#define DEBUG_H

/**
 * This module contains methods used for debugging the board.
 * 
 * @authors Thomas M.
*/

#include <Arduino.h>

/**
 * Setup method called by the main task to initialise any resources needed by
 * the debug subsystem.
*/
void setupDebug();

///////////////////////////////////////
//// OLED METHODS
/**
 * Draw a string at the given coordinates on the display.  
 * Call dbg_display() to blit the display buffer to the screen.
*/
void dbg_displayString(int16_t x, int16_t y, String strUser);

/**
 * Set an individual pixel at the given coordinates on the display.  
 * Call dbg_display() to blit the display buffer to the screen.
*/
void dbg_displaySetPixel(int16_t x, int16_t y);

/**
 * Clear the screen
*/
void dbg_clearDisplay();

/**
 * Blit the contents of the internal display buffer to the screen.
*/
void dbg_display();

/**
 * Send a binary buffer to the serial port.
 * 
 * @param buffer the buffer to send
 * @param length how many bytes to send
*/
void dbg_send_buffer(const uint8_t* buffer, size_t length);

/**
 * Send a binary buffer to the serial port.
 * 
 * @param buffer the buffer to send
 * @param length how many bytes to send
*/
void dbg_send_buffer(const float* buffer, size_t length);

/**
 * This method can be used to fill a buffer with a sine wave for debugging purposes.
 * 
 * Many values are hardcoded in this method because of it's limited use.
 * 
 * @param buff the buffer to fill
 * @param window_index the index of the buffer window to fill
 * @param freq the frequency in Hz to produce
*/
void dbg_fill_sine_wave(uint16_t* buff, int window_index, float freq);

///////////////////////////////////////
//// SERIAL LOGGING
/**
 * C-macro based logging library by Thomas M. To use, simply include this header and call:
 *  - dbg_log(format, args...)
 *  - dbg_log_info(format, args...)
 *  - dbg_log_warning(format, args...)
 *  - dbg_log_fatal(format, args...)
 * As methods in your code.
 * In the config file define:
 *   #define DEBUG_SERIAL_LOGGING
 * To enable the serial logging subsystem
 * And define whichever level of logging you want to enable, note that enabling
 * a less severe level of logging also enables all the more severe ones:
 *   #define DEBUG_WARNING
 * Will enable both warnings and fatal error logging.
*/
#if defined(DEBUG_SERIAL_LOGGING) && (defined(DEBUG_FATAL) || defined(DEBUG_WARNING) || defined(DEBUG_INFO) || defined(DEBUG_VERBOSE))
/**
 * Log a fatal error to the serial port. Appends a new line at the end of the message.
 * 
 * @param format the string format to print (format must be a string)
 * @param ... any additional parameters to be formatted into the string
*/
#define dbg_log_fatal(format, ...) Serial.printf("[FATAL] " format "\n", ## __VA_ARGS__)
#else
#define dbg_log_fatal
#endif

#if defined(DEBUG_SERIAL_LOGGING) && (defined(DEBUG_WARNING) || defined(DEBUG_INFO) || defined(DEBUG_VERBOSE))
/**
 * Log a warning to the serial port. Appends a new line at the end of the message.
 * 
 * @param format the string format to print (format must be a string)
 * @param ... any additional parameters to be formatted into the string
*/
#define dbg_log_warning(format, ...) Serial.printf("[WARNING] " format "\n", ## __VA_ARGS__)
#else
#define dbg_log_warning
#endif

#if defined(DEBUG_SERIAL_LOGGING) && (defined(DEBUG_INFO) || defined(DEBUG_VERBOSE))
/**
 * Log a message to the serial port. Appends a new line at the end of the message.
 * 
 * @param format the string format to print (format must be a string)
 * @param ... any additional parameters to be formatted into the string
*/
#define dbg_log_info(format, ...) Serial.printf("[INFO] " format "\n", ## __VA_ARGS__)
#else
#define dbg_log_info
#endif

#if defined(DEBUG_SERIAL_LOGGING) && defined(DEBUG_VERBOSE)
/**
 * Log a debugging message to the serial port. *Doesn't* append a new line at the end of the message.
 * 
 * @param format the string format to print
 * @param ... any additional parameters to be formatted into the string
*/
#define dbg_log Serial.printf
#else
#define dbg_log
#endif

#endif // DEBUG_H