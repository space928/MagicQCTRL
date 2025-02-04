#ifndef HID_H
#define HID_H

void initHID();

void sendKey(uint8_t page, uint8_t keyCode, bool value);

void sendButton(uint8_t page, uint8_t buttonCode, bool value);

void sendEncoder(uint8_t page, uint8_t encoderId, int8_t delta);

#endif //HID_H
