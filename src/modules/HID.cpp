#include "Adafruit_TinyUSB.h"
#include "Config.h"
#include "Globals.h"

union { uint32_t packed; uint8_t bytes[4]; } packed_bytes;

// ===================================
// CONSTS
#define USB_MSG_REPORT_SIZE 64

// HID report descriptor using TinyUSB's template
// Generic In Out with 64 bytes report (max)
uint8_t const desc_hid_report[] =
{
	TUD_HID_REPORT_DESC_GENERIC_INOUT(USB_MSG_REPORT_SIZE)
};

uint8_t report_buffer[USB_MSG_REPORT_SIZE] = {0};

// ===================================
// LOCALS

// USB HID object. 
// desc report, desc len, protocol, interval, use out endpoint
Adafruit_USBD_HID usb_hid(desc_hid_report, sizeof(desc_hid_report), HID_ITF_PROTOCOL_NONE, 2, true);

// ===================================
// METHODS
// Invoked when received SET_REPORT control request or
// received data on OUT endpoint ( Report ID = 0, Type = 0 )
void set_report_callback(uint8_t report_id, hid_report_type_t report_type, uint8_t const* buffer, uint16_t bufsize)
{
	// This example doesn't use multiple report and report ID
	(void) report_id;
	(void) report_type;

	// echo back anything we received from host
	// usb_hid.sendReport(0, buffer, bufsize);
}

void initHID()
{
	//usb_hid.enableOutEndpoint(true);
	//usb_hid.setPollInterval(2);
	//usb_hid.setReportDescriptor(desc_hid_report, sizeof(desc_hid_report));
	usb_hid.setStringDescriptor("MagicQ CTRL");

	//usb_hid.setReportCallback(NULL, set_report_callback);
	usb_hid.begin();

	// Wait until device mounted
	while( !TinyUSBDevice.mounted() ) delay(1);
}

/*
 * The message structs are packed as follows:
 * keyMsg = {
 *  bit key0_on,
 *  bit key0_off,
 *  bit key1_on,
 *  bit key1_off, 
 *  bit key2_on,
 *  bit key2_off, 
 *  ... till key11
 * }
 * 
 * buttonMsg = {
 *   bit button0_on,
 *   bit button0_off,
 *   ... until button 8
 * }
 * 
 * encodersA = {
 *   sbyte encoder0_delta,
 *   sbyte encoder1_delta,
 *   ... until encoder7
 * }
 * 
 * The usb report is simply these 5 ints concatenated together
 */
void sendMessage(uint32_t page, uint32_t keyMsg, uint32_t buttonMsg, uint32_t encodersA, uint32_t encodersB)
{
	uint8_t* ptr = report_buffer;
	report_buffer[0] = 0xFA;
	report_buffer[1] = 0xDE;
	memcpy(ptr+4, &page, sizeof(page));
	memcpy(ptr+8, &keyMsg, sizeof(keyMsg));
	memcpy(ptr+12, &buttonMsg, sizeof(buttonMsg));
	memcpy(ptr+16, &encodersA, sizeof(encodersA));
	memcpy(ptr+24, &encodersB, sizeof(encodersB));

	Serial.printf("USB msg1: FADE%4x%4x%4x%4x%4x\n", page, keyMsg, buttonMsg, encodersA, encodersB);
	//report_buffer[0] = 
	
	usb_hid.sendReport(0, report_buffer, USB_MSG_REPORT_SIZE);
}

void sendKey(int page, int keyCode, bool value)
{
	sendMessage(page, ((uint32_t)1 << ((uint32_t)keyCode+1)) + (value?0:1), 0, 0, 0);
}

void sendButton(int page, int buttonCode, bool value)
{
	sendMessage(page, 0, (1 << (buttonCode+1)) + value?0:1, 0, 0);
}

void sendEncoder(int page, int encoderId, int8_t delta)
{
	if(encoderId < 4)
		sendMessage(page, 0, 0, delta << (encoderId * sizeof(delta)), 0);
	else
		sendMessage(page, 0, 0, 0, delta << ((encoderId - 4) * sizeof(delta)));
}
