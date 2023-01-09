#include <Adafruit_SH110X.h>
#include "Config.h"
#include "Globals.h"


// ===================================
// GLOBALS
// Create the OLED display
Adafruit_SH1106G display = Adafruit_SH1106G(128, 64, &SPI1, OLED_DC, OLED_RST, OLED_CS);

int display_page = 0;

// ===================================
// LOCALS
int display_last_on = 0;

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
}

void drawPage() {
  display.setCursor(0, 8);
  display.print("## Page ");
  display.print(display_page + 1);
  display.print(" ##");
}

void drawSetup() {
  display.setCursor(0, 8);
  display.print("## SETUP ##");
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
    display.setCursor(0, 0);
    display.println("# MagicQ CTRL V0.1 #");

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

void setPage(int page) {
  if(page < -ENABLE_DEBUG_PAGE) {
    page = -ENABLE_DEBUG_PAGE;
  } else if(page >= N_PAGES) {
    // There are always N_PAGES+1 pages in total as the last page is the setup page
    page = N_PAGES;
  }

  display_page = page;
}

void changePage(int dir) {
  if(display_page + dir >= -ENABLE_DEBUG_PAGE && display_page + dir <= N_PAGES) {
    display_page += dir;
  }
}
