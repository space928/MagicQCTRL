#include "Config.h"
#include "debug.h"
#include "Globals.h"
#include "Display.h"
//#include <Adafruit_SH110X.h>

// ===================================
// LOCALS
int16_t xpos = DISPLAY_WIDTH / 2;
int16_t ypos = DISPLAY_HEIGHT / 2;

// ===================================
// FUNCTIONS

void initSketch()
{
    auto_clear_display = false;
    display.clearDisplay();
}

void drawSketch()
{
    int16_t nx, ny;
    nx = xpos - min(max(encoder_dirs[3], -4), 4);
    ny = ypos + encoder_dirs[7];

    if (nx != xpos || ny != ypos)
    {
        display.drawLine(xpos, ypos, nx, ny, 1);
        xpos = min(max(nx, 0), DISPLAY_WIDTH-1);
        ypos = min(max(ny, 0), DISPLAY_HEIGHT-1);
        display.display();
    }

    pixels.clear();
    pixels.setPixelColor(9, 10, 30, 255); // Clear
    pixels.setPixelColor(2, 200, 0, 5); // Close

    if (switch_states[9])
    {
        display.clearDisplay();
        xpos = DISPLAY_WIDTH / 2;
        ypos = DISPLAY_HEIGHT / 2;
    }

    if (switch_states[2])
    {
        auto_clear_display = true;
        display_page = N_PAGES;
    }

    delay(16);
}
