#include <Adafruit_SH110X.h>
#include "Config.h"
#include "debug.h"
#include "Globals.h"
#include "EncoderKeysIO.h"


// ===================================
// GLOBALS
// Create the OLED display
Adafruit_SH1106G display = Adafruit_SH1106G(128, 64, &SPI1, OLED_DC, OLED_RST, OLED_CS);

int8_t display_page = 0;
uint8_t key_brightness = DEFAULT_BRIGHTNESS;
uint8_t display_contrast = DEFAULT_BRIGHTNESS;

// ===================================
// LOCALS
int display_last_on = 0;
char switch_names[N_PAGES][N_KEYS][KEY_NAME_LENGTH+1];
char encoder_names[N_PAGES][N_ENCODERS][KEY_NAME_LENGTH+1];
unsigned long test_start_time = 0;

// ===================================
// METHODS
void setupDisplay() {
    // Start OLED
    display.begin(0, true); // we dont use the i2c address but we will reset!
    display.display();
    display.setContrast(display_contrast);

    // text display tests
    display.setTextSize(1);
    display.setTextWrap(false);
    display.setTextColor(SH110X_WHITE, SH110X_BLACK); // white text, black background

    display.clearDisplay();
    display.setCursor(0, 0);
    display.println("MagicQ CTRL V" VERSION_STRING);
    display.setCursor(0, 8);
    display.println("Waiting for USB...");

    display.display();
}

void drawFilledRect(uint8_t x, uint8_t y, uint8_t w, uint8_t h, uint16_t colour) {
    for(int y1 = y; y1 < y+h; y1++)
        display.drawFastHLine(x, y1, w, colour);
}

void setKeyName(uint8_t page, uint8_t keyId, uint8_t isEncoder, char const (&name)[KEY_NAME_LENGTH]) {
    char* ptr;//[KEY_NAME_LENGTH];
    if(isEncoder == 0)
        ptr = switch_names[page][keyId];
    else
        ptr = encoder_names[page][keyId];
    memcpy(ptr, name, KEY_NAME_LENGTH);
}

void drawPage() {
    display.setCursor(4, 8);
    display.print("###### Page ");
    display.print(display_page + 1);
    display.print(" ######");

    if(encoder_pressed || encoders_active)
    {
        for (int i = 0; i < N_ENCODERS; i++) {
            if (encoder_switches[i]) {
                drawFilledRect((i / (N_ENCODERS / 2)) * (DISPLAY_WIDTH - 4 - KEY_NAME_LENGTH * 6), 
                               24 + (i % (N_ENCODERS / 2)) * 10 - 1, 
                               KEY_NAME_LENGTH * 6 + 1, 
                               10, 
                               1);
                display.setTextColor(0, 0);
            } else {
                display.setTextColor(1, 0);
            }

            if (encoder_dirs[i] != 0) {
                // Encoder direction preview
                uint8_t x = (i / (N_ENCODERS / 2)) * (DISPLAY_WIDTH - 4 - KEY_NAME_LENGTH * 6) + KEY_NAME_LENGTH * 3 - max(encoder_dirs[i], 0);
                uint8_t w = abs(encoder_dirs[i]) + 1;
                if (w != 0)
                    display.drawFastHLine(x, 32+(i % (N_ENCODERS / 2)) * 10, w, 1);
            }

            uint8_t lbl_len = strlen(encoder_names[display_page][i]);
            display.setCursor((i / (N_ENCODERS / 2)) * (DISPLAY_WIDTH - 4 - lbl_len * 6) + 2, 
                              24 + (i % (N_ENCODERS / 2)) * 10);
            display.print(encoder_names[display_page][i]);
            display.setTextColor(1, 0);
        }
    } else {
        for (int i = 0; i < N_KEYS; i++) {
            if (switch_states[i]) {
                drawFilledRect((i % 3) * 42, 24 + (i / 3) * 10 - 1, KEY_NAME_LENGTH * 6 + 1, 10, 1);
                display.setTextColor(0, 0);
            } else {
                display.setTextColor(1, 0);
            }
            display.setCursor((i % 3) * 42 + 1, 24 + (i / 3) * 10);
            display.print(switch_names[display_page][i]);
        }
        display.setTextColor(1, 0);
    }
}

void drawKeyText(int keyInd, String text) {
    display.setCursor((keyInd % 3) * 42 + 1, 24 + (keyInd / 3) * 10);
    display.print(text);
}

void drawSetup() {
    display.setCursor(4, 8);
    display.print("###### ");
    display.setCursor(4 + 5*8 + 6, 8);
    display.print("SETUP");
    display.setCursor(DISPLAY_WIDTH - 6 - 5*8, 8);
    display.print(" ######");

    drawKeyText(0, "Debug");
    if(switch_states[0])
        display_page = -1;

    drawKeyText(1, "Test");
    if(switch_states[1]) {
        test_start_time = millis();
        display_page = -2;
    }

    // Key brightness control
    if(encoder_dirs[4] != 0) {
        key_brightness = (uint8_t)min(max(key_brightness - (int16_t)encoder_dirs[4], 0), 255);
        pixels.setBrightness(key_brightness);
    }
    if(encoders_active)
        drawKeyText(2, String(key_brightness));
    else
        drawKeyText(2, "Bright");

    drawKeyText(3, "SrtAni");
    if(switch_states[3])
        startupAnim();

    // Display brightness control
    if(encoder_dirs[5] != 0) {
        display_contrast = (uint8_t)min(max(display_contrast - (int16_t)encoder_dirs[5], 0), 255);
        display.setContrast(display_contrast);
    }
    if(encoders_active)
        drawKeyText(5, String(display_contrast));
    else
        drawKeyText(5, "Cntrst");
}

void drawDebug() {
    display.setCursor(0, 8);
    display.print("Rotary encoder: ");
    display.print(encoder_pos);

    /*display.setCursor(0, 16);
    display.print("I2C Scan: ");
    for (uint8_t address = 0; address <= 0x7F; address++) {
        if (!i2c_found[address]) continue;
        display.print("0x");
        display.print(address, HEX);
        display.print(" ");
    }*/

    // check encoder press
    display.setCursor(0, 24);
    if (!digitalRead(PIN_SWITCH)) {
        display.print("Encoder pressed ");
    } 

    // move the text into a 3x4 grid
    display.setCursor(0, 32);
    //display.print("Key states: ");
    for (int i = 0; i < 12; i++) {
        display.drawRect(52 + (i % 3) * 8, 32 + (i / 3) * 8, 8, 8, 1);
        drawFilledRect(52 + (i % 3) * 8 + 1, 32 + (i / 3) * 8 + 1, 6, 6, switch_states[i]?1:0);
    }

    for (int i = 0; i < 8; i++) {
        display.drawRect(40 + (i/4) * 40, 32 + (i%4) * 8, 8, 8, 1);
        drawFilledRect(40 + (i/4) * 40 + 1, 32 + (i%4) * 8 + 1, 6, 6, (encoder_switches[i]&1)==1?1:0);
    }

    for (int i = 0; i < 8; i++) {
        display.setCursor((DISPLAY_WIDTH-8*3)*(i/4), 32+(i%4)*8);
        display.print(encoder_dirs[i]);
        // Encoder direction preview
        uint8_t x = (DISPLAY_WIDTH-8*3)*(i/4) + KEY_NAME_LENGTH * 3 - max(encoder_dirs[i], 0);
        uint8_t w = abs(encoder_dirs[i]) + 1;
        display.drawFastHLine(x, 32+(i%4)*8+7, w, 1);
    }
}

void drawTest() {
    display.setCursor(0, 48);
    display.print("(c) Thomas Mathieson");
    display.setCursor(DISPLAY_WIDTH/2 - 15*3, 56);
    display.print("[mathieson.dev]");

    unsigned long t = millis() - test_start_time;
    int16_t lastY = (int16_t)(((sin((t/10)/(float)5))+1)*32);
    for(int i = 0; i < DISPLAY_WIDTH-2; i += 2) {
        int16_t y = (int16_t)(((sin((i+t/10)/(float)5))+1)*32);
        display.drawLine(i, lastY, i+2, y, 1);
        lastY = y;
    }

    if(encoder_pressed) {
        display_page = N_PAGES;
    }
}

void updateDisplay() {
    display.clearDisplay();
    
    if(millis() - display_last_on < DISPLAY_TIMEOUT) {
        display.setCursor(4, 0);
        display.print("# MagicQ CTRL V" VERSION_STRING " #");

        if (display_page < 0) {
            switch (display_page)
            {
            case -1:
                drawDebug();
                break;
            case -2:
                drawTest();
                break;
            default:
                display.setCursor(4, 8);
                display.print("Page ");
                display.print(display_page + 1);
                break;
            }
        } else if (display_page == N_PAGES) {
            drawSetup();
        } else {
            drawPage();
        }
    }

    // display oled
    display.display();
}

void wakeDisplay() {
    display_last_on = millis();
}

void setPage(int8_t page) {
    if(page < -ENABLE_DEBUG_PAGE) {
        page = -ENABLE_DEBUG_PAGE;
    } else if(page >= N_PAGES) {
        // There are always N_PAGES+1 pages in total as the last page is the setup page
        page = N_PAGES;
    }

    display_page = page;
}

void changePage(int8_t dir) {
    if(display_page + dir >= -ENABLE_DEBUG_PAGE && display_page + dir <= N_PAGES) {
        display_page += dir;
    }
}
