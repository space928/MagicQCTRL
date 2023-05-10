#include <Adafruit_SH110X.h>
#include "Config.h"
#include "debug.h"
#include "Globals.h"


// ===================================
// GLOBALS
// Create the OLED display
Adafruit_SH1106G display = Adafruit_SH1106G(128, 64, &SPI1, OLED_DC, OLED_RST, OLED_CS);

int8_t display_page = 0;

// ===================================
// LOCALS
int display_last_on = 0;
char switch_names[N_PAGES][N_KEYS][KEY_NAME_LENGTH+1];
char encoder_names[N_PAGES][N_ENCODERS][KEY_NAME_LENGTH+1];

// ===================================
// METHODS
void setupDisplay() {
    // Start OLED
    display.begin(0, true); // we dont use the i2c address but we will reset!
    display.display();

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

    if(encoder_pressed)
    {
        for (int i = 0; i < N_ENCODERS; i++) {
            display.setCursor((i / (N_ENCODERS / 2)) * (DISPLAY_WIDTH - 2 - KEY_NAME_LENGTH * 6) + 2, 24 + (i % (N_ENCODERS / 2)) * 10);
            display.print(encoder_names[display_page][i]);
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

void drawSetup() {
    display.setCursor(4, 8);
    display.print("###### ");
    display.setCursor(4 + 5*8 + 6, 8);
    display.print("SETUP");
    display.setCursor(DISPLAY_WIDTH - 6 - 5*8, 8);
    display.print(" ######");
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
    for (int i = 0; i < 12; i++) {
        if(switch_states[i]) {
            display.setCursor((i % 3) * 48, 32 + (i / 3) * 8);
            display.print("KEY");
            display.print(i+1);
        }
    }
}

void updateDisplay() {
    display.clearDisplay();
    
    if(millis() - display_last_on < DISPLAY_TIMEOUT) {
        display.setCursor(4, 0);
        display.print("# MagicQ CTRL V" VERSION_STRING " #");

        if (display_page < 0) {
            drawDebug();
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
