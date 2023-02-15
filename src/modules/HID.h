#ifndef HID_H
#define HID_H

void initHID();

void sendKey(int page, int keyCode, bool value);

void sendButton(int page, int buttonCode, bool value);

void sendEncoder(int page, int encoderId, int8_t delta);

#endif //HID_H
