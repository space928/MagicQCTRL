/**
 * This module contains methods used for debugging the board.
 * 
 * @authors Thomas M.
*/

#include "debug.h"
#include "config.h"
#include <Arduino.h>

/**
 * Setup method called by the main task to initialise any resources needed by
 * the debug subsystem.
*/
void setupDebug()
{
    #ifdef DEBUG_SERIAL_LOGGING
    Serial.begin(115200);
    #endif

    #ifdef DEBUG_DISPLAY_ENABLE
    // Setup the OLED
    pinMode(Vext, OUTPUT);
    digitalWrite(Vext, LOW);
    if(!display.begin(SSD1306_SWITCHCAPVCC, DEBUG_SCREEN_ADDRESS)) {
        dbg_log_fatal("SSD1306 allocation failed");
        for(;;) delay(10); // Don't proceed, loop forever
    }

    display.clearDisplay();
    display.display();
    #endif
}

///////////////////////////////////////
//// OLED METHODS
/**
 * Draw a string at the given coordinates on the display.  
 * Call dbg_display() to blit the display buffer to the screen.
*/
void dbg_displayString(int16_t x, int16_t y, String strUser) 
{
    #ifdef DEBUG_DISPLAY_ENABLE
    for(int i = 0; i < strUser.length(); i++)
        display.drawChar(x + i * 8, y, strUser.charAt(i), SSD1306_INVERSE, SSD1306_INVERSE, 1);
    #endif
}

/**
 * Set an individual pixel at the given coordinates on the display.  
 * Call dbg_display() to blit the display buffer to the screen.
*/
void dbg_displaySetPixel(int16_t x, int16_t y) 
{
    #ifdef DEBUG_DISPLAY_ENABLE
    display.drawPixel(x, y, SSD1306_INVERSE);
    #endif
}

/**
 * Clear the screen
*/
void dbg_clearDisplay()
{
    #ifdef DEBUG_DISPLAY_ENABLE
    display.clearDisplay();
    display.display();
    #endif
}

/**
 * Blit the contents of the internal display buffer to the screen.
*/
void dbg_display()
{
    #ifdef DEBUG_DISPLAY_ENABLE
    display.display();
    #endif
}

/**
 * Send a binary buffer to the serial port.
 * 
 * @param buffer the buffer to send
 * @param length how many bytes to send
*/
void dbg_send_buffer(const uint8_t* buffer, size_t length)
{
    #ifdef DEBUG_SERIAL_LOGGING
    Serial.write(buffer, length);
    #endif
}

/**
 * Send a binary buffer to the serial port.
 * 
 * @param buffer the buffer to send
 * @param length how many bytes to send
*/
void dbg_send_buffer(const float* buffer, size_t length)
{
    #ifdef DEBUG_SERIAL_LOGGING
    Serial.write(reinterpret_cast<const uint8_t*>(buffer), length);
    #endif
}

// https://codereview.stackexchange.com/a/78539
constexpr char hexmap[] = {'0', '1', '2', '3', '4', '5', '6', '7',
                           '8', '9', 'a', 'b', 'c', 'd', 'e', 'f'};

/**
 * Converts a byte array to a hex string, useful for debugging.
*/
std::string hexStr2(const uint8_t *data, int len)
{
    std::string s(len * 2, ' ');
    for (int i = 0; i < len; ++i) {
        s[2 * i]     = hexmap[(data[i] & 0xF0) >> 4];
        s[2 * i + 1] = hexmap[data[i] & 0x0F];
    }
    return s;
}
