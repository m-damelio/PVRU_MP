// Libraries
#include <ezButton.h>
#include <BleKeyboard.h>

BleKeyboard bleKeyboard;

// Encoder 1 pins
#define ENC1_CLK_PIN 25
#define ENC1_DT_PIN  26
#define ENC1_SW_PIN  27

// Encoder 2 pins
#define ENC2_CLK_PIN 14
#define ENC2_DT_PIN  15
#define ENC2_SW_PIN  13

// Encoder 3 pins
#define ENC3_CLK_PIN 4
#define ENC3_DT_PIN  16
#define ENC3_SW_PIN  17

// Encoder 4 pins
#define ENC4_CLK_PIN 5
#define ENC4_DT_PIN  18
#define ENC4_SW_PIN  19

// WASD pins
#define W_PIN 21
#define A_PIN 22
#define S_PIN 23
#define D_PIN 32

// State variables
int enc1_CLK_state, enc1_prev_CLK_state;
int enc2_CLK_state, enc2_prev_CLK_state;
int enc3_CLK_state, enc3_prev_CLK_state;
int enc4_CLK_state, enc4_prev_CLK_state;

// Button objects
ezButton button1(ENC1_SW_PIN);
ezButton button2(ENC2_SW_PIN);
ezButton button3(ENC3_SW_PIN);
ezButton button4(ENC4_SW_PIN);

// WASD buttons
ezButton buttonW(W_PIN);
ezButton buttonA(A_PIN);
ezButton buttonS(S_PIN);
ezButton buttonD(D_PIN);

void setup() {
  Serial.begin(19200);
  
  // Init BLE
  bleKeyboard.begin();

  // Setup encoder 1
  pinMode(ENC1_CLK_PIN, INPUT);
  pinMode(ENC1_DT_PIN, INPUT);
  button1.setDebounceTime(50);
  enc1_prev_CLK_state = digitalRead(ENC1_CLK_PIN);
  
  // Setup encoder 2
  pinMode(ENC2_CLK_PIN, INPUT);
  pinMode(ENC2_DT_PIN, INPUT);
  button2.setDebounceTime(50);        
  enc2_prev_CLK_state = digitalRead(ENC2_CLK_PIN);
  
  // Setup encoder 3
  pinMode(ENC3_CLK_PIN, INPUT);
  pinMode(ENC3_DT_PIN, INPUT);
  button3.setDebounceTime(50);
  enc3_prev_CLK_state = digitalRead(ENC3_CLK_PIN);
  
  // Setup encoder 4
  pinMode(ENC4_CLK_PIN, INPUT);
  pinMode(ENC4_DT_PIN, INPUT);
  button4.setDebounceTime(50);
  enc4_prev_CLK_state = digitalRead(ENC4_CLK_PIN);
  
  // Setup WASD buttons
  pinMode(W_PIN, INPUT_PULLUP);
  pinMode(A_PIN, INPUT_PULLUP);
  pinMode(S_PIN, INPUT_PULLUP);
  pinMode(D_PIN, INPUT_PULLUP);
  buttonW.setDebounceTime(50);
  buttonA.setDebounceTime(50);
  buttonS.setDebounceTime(50);
  buttonD.setDebounceTime(50);
  
  // Print info
  Serial.println("Multi-Encoder + WASD Controller Ready!");
  Serial.println("Encoders:");
  Serial.println("  Encoder 1 (25,26,27): Left/Right arrows");
  Serial.println("  Encoder 2 (14,12,13): Up/Down arrows");
  Serial.println("  Encoder 3 (4,16,17): N/M keys");
  Serial.println("  Encoder 4 (5,18,19): K/L keys");
  Serial.println("WASD Switches:");
  Serial.println("  Pin 21: W key");
  Serial.println("  Pin 22: A key");
  Serial.println("  Pin 23: S key");
  Serial.println("  Pin 32: D key");
}

void loop() {
  // Update buttons
  button1.loop();
  button2.loop();
  button3.loop();
  button4.loop();
  
  // Update WASD
  buttonW.loop();
  buttonA.loop();
  buttonS.loop();
  buttonD.loop();

  // Handle encoders
  handleEncoder1();
  handleEncoder2();
  handleEncoder3();
  handleEncoder4();
  
  // Check button presses
  checkButtons();
  
  // Check WASD
  checkWASDSwitches();
}

// Left/Right arrows
void handleEncoder1() {
  enc1_CLK_state = digitalRead(ENC1_CLK_PIN);
  
  if (enc1_CLK_state != enc1_prev_CLK_state && enc1_CLK_state == HIGH) {
    if (digitalRead(ENC1_DT_PIN) == HIGH) {
      // Left arrow
      if (bleKeyboard.isConnected()) {
        bleKeyboard.press(KEY_LEFT_ARROW);
        delay(10);
        bleKeyboard.releaseAll();
      }
      Serial.println("Encoder 1: Left Arrow");
    } else {
      // Right arrow
      if (bleKeyboard.isConnected()) {
        bleKeyboard.press(KEY_RIGHT_ARROW);
        delay(10);
        bleKeyboard.releaseAll();
      }
      Serial.println("Encoder 1: Right Arrow");
    }
  }
  enc1_prev_CLK_state = enc1_CLK_state;
}

// Up/Down arrows
void handleEncoder2() {
  enc2_CLK_state = digitalRead(ENC2_CLK_PIN);
  
  if (enc2_CLK_state != enc2_prev_CLK_state && enc2_CLK_state == HIGH) {
    if (digitalRead(ENC2_DT_PIN) == HIGH) {
      // Down arrow
      if (bleKeyboard.isConnected()) {
        bleKeyboard.press(KEY_DOWN_ARROW);
        delay(10);
        bleKeyboard.releaseAll();
      }
      Serial.println("Encoder 2: Down Arrow");
    } else {
      // Up arrow
      if (bleKeyboard.isConnected()) {
        bleKeyboard.press(KEY_UP_ARROW);
        delay(10);
        bleKeyboard.releaseAll();
      }
      Serial.println("Encoder 2: Up Arrow");
    }
  }
  enc2_prev_CLK_state = enc2_CLK_state;
}

// N/M keys
void handleEncoder3() {
  enc3_CLK_state = digitalRead(ENC3_CLK_PIN);
  
  if (enc3_CLK_state != enc3_prev_CLK_state && enc3_CLK_state == HIGH) {
    if (digitalRead(ENC3_DT_PIN) == HIGH) {
      // N key
      if (bleKeyboard.isConnected()) {
        bleKeyboard.press('n');
        delay(10);
        bleKeyboard.releaseAll();
      }
      Serial.println("Encoder 3: N key");
    } else {
      // M key
      if (bleKeyboard.isConnected()) {
        bleKeyboard.press('m');
        delay(10);
        bleKeyboard.releaseAll();
      }
      Serial.println("Encoder 3: M key");
    }
  }
  enc3_prev_CLK_state = enc3_CLK_state;
}

// K/L keys
void handleEncoder4() {
  enc4_CLK_state = digitalRead(ENC4_CLK_PIN);
  
  if (enc4_CLK_state != enc4_prev_CLK_state && enc4_CLK_state == HIGH) {
    if (digitalRead(ENC4_DT_PIN) == HIGH) {
      // K key
      if (bleKeyboard.isConnected()) {
        bleKeyboard.press('k');
        delay(10);
        bleKeyboard.releaseAll();
      }
      Serial.println("Encoder 4: K key");
    } else {
      // L key
      if (bleKeyboard.isConnected()) {
        bleKeyboard.press('l');
        delay(10);
        bleKeyboard.releaseAll();
      }
      Serial.println("Encoder 4: L key");
    }
  }
  enc4_prev_CLK_state = enc4_CLK_state;
}

// Encoder buttons
void checkButtons() {
  if (button1.isPressed()) {
    Serial.println("Encoder 1 button pressed");
  }
  
  if (button2.isPressed()) {
    Serial.println("Encoder 2 button pressed");
  }
  
  if (button3.isPressed()) {
    Serial.println("Encoder 3 button pressed");
  }
  
  if (button4.isPressed()) {
    Serial.println("Encoder 4 button pressed");
  }
}

// WASD buttons
void checkWASDSwitches() {
  if (buttonW.isPressed()) {
    if (bleKeyboard.isConnected()) {
      bleKeyboard.press('w');
      delay(10);
      bleKeyboard.releaseAll();
    }
    Serial.println("W key pressed");
  }
  
  if (buttonA.isPressed()) {
    if (bleKeyboard.isConnected()) {
      bleKeyboard.press('a');
      delay(10);
      bleKeyboard.releaseAll();
    }
    Serial.println("A key pressed");
  }
  
  if (buttonS.isPressed()) {
    if (bleKeyboard.isConnected()) {
      bleKeyboard.press('s');
      delay(10);
      bleKeyboard.releaseAll();
    }
    Serial.println("S key pressed");
  }
  
  if (buttonD.isPressed()) {
    if (bleKeyboard.isConnected()) {
      bleKeyboard.press('d');
      delay(10);
      bleKeyboard.releaseAll();
    }
    Serial.println("D key pressed");
  }
}