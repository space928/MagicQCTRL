#ifndef ENCODERKEYSIO_H
#define ENCODERKEYSIO_H

void setupIO();

void startupAnim();

void setKeyCol(uint8_t keyId, uint32_t colLow, uint32_t colHigh);

void tickIO();

#endif // ENCODERKEYSIO_H