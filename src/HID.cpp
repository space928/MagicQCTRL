#include "Adafruit_TinyUSB.h"
#include "Config.h"
#include "Globals.h"
#include "debug.h"
#include "EncoderKeysIO.h"
#include "Display.h"

union
{
    uint32_t packed;
    uint8_t bytes[4];
} packed_bytes;

// ===================================
// CONSTS
#define USB_MSG_REPORT_SIZE 64

// HID report descriptor using TinyUSB's template
// Generic In Out with 64 bytes report (max)
uint8_t const desc_hid_report[] = {TUD_HID_REPORT_DESC_GENERIC_INOUT(USB_MSG_REPORT_SIZE)};

uint8_t report_buffer[USB_MSG_REPORT_SIZE] = {0};

// ===================================
// LOCALS

// USB HID object.
// desc report, desc len, protocol, interval, use out endpoint
Adafruit_USBD_HID usb_hid(desc_hid_report, sizeof(desc_hid_report), HID_ITF_PROTOCOL_NONE, 2, true);

enum USBRXMessageType : uint8_t
{
    None = 0,
    Colour = 1,
    Name = 2,
};

struct USBColourMessageData
{
    uint8_t colLowR;
    uint8_t colLowG;
    uint8_t colLowB;
    uint8_t colHighR;
    uint8_t colHighG;
    uint8_t colHighB;
};

struct USBKeyNameMessageData
{
    char name[KEY_NAME_LENGTH];
};

struct USBRXMessage
{
    USBRXMessageType msgType;
    int8_t page;
    uint8_t keyId;
    uint8_t isEncoder;
    union
    {
        USBColourMessageData colour;
        USBKeyNameMessageData name;
    };
};

// ===================================
// METHODS
// Invoked when received SET_REPORT control request or
// received data on OUT endpoint ( Report ID = 0, Type = 0 )
void set_report_callback(uint8_t report_id, hid_report_type_t report_type, uint8_t const *buffer, uint16_t bufsize)
{
    // This example doesn't use multiple report and report ID
    (void)report_id;
    (void)report_type;

    // dbg_log("Recv USB: %s\n", hexStr2(buffer, bufsize).c_str());

    if (bufsize >= sizeof(USBRXMessage))
    {
        USBRXMessage msg;
        memcpy(&msg, buffer, sizeof(USBRXMessage));

        switch (msg.msgType)
        {
        case USBRXMessageType::Colour:
            setKeyCol(msg.keyId + msg.page * N_KEYS,
                      msg.colour.colLowB + (msg.colour.colLowG << 8) + (msg.colour.colLowR << 16),
                      msg.colour.colHighB + (msg.colour.colHighG << 8) + (msg.colour.colHighR << 16));
            break;

        case USBRXMessageType::Name:
            setKeyName(msg.page, msg.keyId, msg.isEncoder, msg.name.name);
            break;

        default:
            dbg_log_warning("Invalid USB message type: %n", msg.msgType);
            break;
        }
    }
}

void initHID()
{
    // usb_hid.enableOutEndpoint(true);
    // usb_hid.setPollInterval(2);
    // usb_hid.setReportDescriptor(desc_hid_report, sizeof(desc_hid_report));
    usb_hid.setStringDescriptor("MagicQ CTRL");

    usb_hid.setReportCallback(NULL, set_report_callback);
    usb_hid.begin();

    // Wait until device mounted
    while (!TinyUSBDevice.mounted())
        delay(1);
}

void sendKey(uint8_t page, uint8_t keyCode, bool value)
{
    report_buffer[0] = 0xFA;
    report_buffer[1] = 0xDE;
    report_buffer[2] = 0x1;
    report_buffer[3] = page;
    report_buffer[4] = keyCode;
    report_buffer[5] = value ? 1 : 0;
    report_buffer[6] = 0xff;

    // dbg_log("USB msg: FADE1%1x%1x%1x\n", page, keyCode, value?1:0);

    usb_hid.sendReport(0, report_buffer, USB_MSG_REPORT_SIZE);
}

void sendButton(uint8_t page, uint8_t buttonCode, bool value)
{
    report_buffer[0] = 0xFA;
    report_buffer[1] = 0xDE;
    report_buffer[2] = 0x2;
    report_buffer[3] = page;
    report_buffer[4] = buttonCode;
    report_buffer[5] = value ? 1 : 0;
    report_buffer[6] = 0xff;

    // dbg_log("USB msg: FADE2%1x%1x%1x\n", page, buttonCode, value?1:0);

    usb_hid.sendReport(0, report_buffer, USB_MSG_REPORT_SIZE);
}

void sendEncoder(uint8_t page, uint8_t encoderId, int8_t delta)
{
    report_buffer[0] = 0xFA;
    report_buffer[1] = 0xDE;
    report_buffer[2] = 0x3;
    report_buffer[3] = page;
    report_buffer[4] = encoderId;
    report_buffer[5] = delta;
    report_buffer[6] = 0xff;

    // dbg_log("USB msg: FADE3%1x%1x%1x\n", page, encoderId, delta);

    usb_hid.sendReport(0, report_buffer, USB_MSG_REPORT_SIZE);
}
