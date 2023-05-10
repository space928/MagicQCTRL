#ifndef DISPLAY_H
#define DISPLAY_H

void setupDisplay();

void setKeyName(uint8_t page, uint8_t keyId, uint8_t isEncoder, char const (&name)[KEY_NAME_LENGTH]);

void updateDisplay();

void wakeDisplay();

void setPage(int8_t page);

void changePage(int8_t dir);

#endif // DISPLAY_H
