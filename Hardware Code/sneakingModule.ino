#include <WiFi.h>
#include <WebServer.h>
#include <ESPmDNS.h>
#include <Adafruit_MPU6050.h>
#include <Wire.h>

Adafruit_MPU6050 mpu;
bool mpuAvailable = true; // accel check

const char* SSID = "AAAAA";
const char* PASS = "AAAAAAAA";
IPAddress local_IP(192, 168, 137, 42);
IPAddress gateway(192, 168, 137, 1);
IPAddress subnet(255, 255, 255, 0);

const char* MDNS_NAME = "a";
const int VIB_PIN = 25;      // vibration pin

bool sneakButtonState = false;
bool inRegion = false;

float motionMagnitude = 0.0;
float smoothedMotion = 0.0;
bool isMoving = false;
const float MOTION_THRESHOLD = 2.0;
const float SNEAK_THRESHOLD = 10.0;
const float SMOOTHING_FACTOR = 0.8;

WebServer server(80); // web server on port 80

const char INDEX_HTML[] PROGMEM = R"rawliteral(
<!DOCTYPE html>
<html>
<head>
<meta name="viewport" content="width=device-width, initial-scale=1">
<style>
body { font-family: Arial; margin: 20px; }
button { padding: 15px 25px; font-size: 18px; margin: 10px; border: none; border-radius: 5px; cursor: pointer; }
#runBtn { background-color: #4CAF50; color: white; width: 200px; height: 80px; }
#runBtn:active { background-color: #45a049; }
#regionBtn { background-color: #2196F3; color: white; }
#onBtn { background-color: #ff9800; color: white; }
#offBtn { background-color: #f44336; color: white; }
.status { font-size: 20px; font-weight: bold; margin: 10px 0; }
</style>
</head>
<body>
<h2>ESP32 VR Sneak Controller</h2>
<div class="status">
  <p>Status: <span id="runState">WALKING</span></p>
</div>
<button id="runBtn" disabled style="background-color: #cccccc; cursor: not-allowed;">
  AUTO-SNEAK MODE
</button><br><br>
<div class="status">
  <p>Motion Speed: <span id="motionSpeed">0.00</span> m/s²</p>
  <p>Movement: <span id="movementState">STILL</span></p>
</div>
<div class="status">
  <p>Region: <span id="regionState">OUT REGION</span></p>
</div>
<button id="regionBtn" onclick="toggleRegion()">TOGGLE REGION</button><br><br>
<h3>Vibration Control:</h3>
<button id="onBtn" onclick="vibrateOn()">VIBRATE ON</button>
<button id="offBtn" onclick="vibrateOff()">VIBRATE OFF</button><br><br>
<div class="status">
  <p>Connection: <span id="connectionState">Checking...</span></p>
</div>
<script>
let sneakState = false;
let inRegion = false;
const updateLabels = () => {
  document.getElementById('runState').textContent = sneakState ? 'SNEAKING' : 'WALKING';
  document.getElementById('runState').style.color = sneakState ? 'red' : 'green';
  document.getElementById('regionState').textContent = inRegion ? 'IN REGION' : 'OUT REGION';
  document.getElementById('regionState').style.color = inRegion ? 'blue' : 'gray';
  document.getElementById('runBtn').style.backgroundColor = sneakState ? '#45a049' : '#4CAF50';
};
const toggleRegion = () => {
  inRegion = !inRegion;
  fetch('/setregion?s=' + (inRegion ? '1' : '0')).catch();
  updateLabels();
};
const vibrateOn = () => { fetch('/vibrate?s=1').catch(); };
const vibrateOff = () => { fetch('/vibrate?s=0').catch(); };
const updateMotionData = () => {
  fetch('/motiondata')
    .then(response => response.json())
    .then(data => {
      document.getElementById('motionSpeed').textContent = data.magnitude.toFixed(2);
      let movementText = 'STILL', movementColor = 'gray';
      if (data.magnitude > 11.0) { movementText = 'WALKING'; movementColor = 'green'; }
      else if (data.magnitude > 2.0) { movementText = 'SNEAKING'; movementColor = 'orange'; }
      document.getElementById('movementState').textContent = movementText;
      document.getElementById('movementState').style.color = movementColor;
      sneakState = data.isSneaking;
      updateLabels();
      document.getElementById('connectionState').textContent = 'Connected';
      document.getElementById('connectionState').style.color = 'green';
    })
    .catch(() => {
      document.getElementById('connectionState').textContent = 'Connection Error';
      document.getElementById('connectionState').style.color = 'red';
    });
};
setTimeout(() => {
  fetch('/sneakstatus')
    .then(response => {
      if (response.ok) {
        document.getElementById('connectionState').textContent = 'Connected';
        document.getElementById('connectionState').style.color = 'green';
      }
    })
    .catch(() => {
      document.getElementById('connectionState').textContent = 'Connection Error';
      document.getElementById('connectionState').style.color = 'red';
    });
}, 1000);
updateLabels();
setInterval(updateMotionData, 200);
document.getElementById('runBtn').addEventListener('contextmenu', e => e.preventDefault());
</script>
</body>
</html>
)rawliteral";

void handleIndex() { server.send_P(200, "text/html", INDEX_HTML); }
void handleVibrate() {
  if (server.hasArg("s")) {
    bool vibrateState = server.arg("s") == "1";
    digitalWrite(VIB_PIN, vibrateState);
  }
  server.send(200, "text/plain", "ok");
}
void handleSneakStatus() {
  String response = sneakButtonState ? "1" : "0";
  server.send(200, "text/plain", response);
}
void handleMotionData() {
  String json = "{";
  json += "\"magnitude\":" + String(smoothedMotion, 2) + ",";
  json += "\"isMoving\":" + String(isMoving ? "true" : "false") + ",";
  json += "\"isSneaking\":" + String(sneakButtonState ? "true" : "false");
  json += "}";
  server.send(200, "application/json", json);
}
void handleSetRegion() {
  if (server.hasArg("s")) {
    inRegion = server.arg("s") == "1";
    if (!sneakButtonState && inRegion && smoothedMotion >= SNEAK_THRESHOLD) {
      digitalWrite(VIB_PIN, HIGH);
    } else {
      digitalWrite(VIB_PIN, LOW);
    }
  }
  server.send(200, "text/plain", "ok");
}
void handleNotFound() { server.send(404, "text/plain", "404 Not Found"); }

void setup() {
  pinMode(VIB_PIN, OUTPUT);
  digitalWrite(VIB_PIN, LOW);
  Serial.begin(115200);

  // WiFi setup
  WiFi.config(local_IP, gateway, subnet);
  WiFi.begin(SSID, PASS);
  int attempts = 0;
  while (WiFi.status() != WL_CONNECTED && attempts < 20) { delay(500); attempts++; }
  if (WiFi.status() == WL_CONNECTED) {
    MDNS.begin(MDNS_NAME);
    // Web server routes
    server.on("/", handleIndex);
    server.on("/vibrate", handleVibrate);
    server.on("/sneakstatus", handleSneakStatus);
    server.on("/setregion", handleSetRegion);
    server.on("/motiondata", handleMotionData);
    server.onNotFound(handleNotFound);
    server.begin();
  }
  while (!Serial) delay(10);
  // sensor setup
  if (!mpu.begin()) {
    mpuAvailable = false; // sensor not found
  } else {
    mpu.setAccelerometerRange(MPU6050_RANGE_8_G);
    mpu.setFilterBandwidth(MPU6050_BAND_5_HZ);
  }
}

void loop() {
  server.handleClient(); // handle web requests
  if (WiFi.status() == WL_CONNECTED) {
    if (mpuAvailable) {
      sensors_event_t a, g, temp;
      mpu.getEvent(&a, &g, &temp);
      motionMagnitude = sqrt(a.acceleration.x * a.acceleration.x + a.acceleration.y * a.acceleration.y + a.acceleration.z * a.acceleration.z);
      smoothedMotion = (smoothedMotion * SMOOTHING_FACTOR) + (motionMagnitude * (1.0 - SMOOTHING_FACTOR));
      isMoving = smoothedMotion > MOTION_THRESHOLD;
    } else {
      // zero if accele not found
      motionMagnitude = 0.0;
      smoothedMotion = 0.0;
      isMoving = false;
    }
    // sneak logic
    bool shouldSneak = isMoving && (smoothedMotion < SNEAK_THRESHOLD);
    bool shouldWalk = smoothedMotion >= SNEAK_THRESHOLD;
    if (shouldSneak && !sneakButtonState) {
      sneakButtonState = true;
      digitalWrite(VIB_PIN, LOW); // turn off vibration when sneaking
    } else if ((shouldWalk || !isMoving) && sneakButtonState) {
      sneakButtonState = false;
      if (shouldWalk && inRegion) digitalWrite(VIB_PIN, HIGH); // vibrate if walking in region
      else digitalWrite(VIB_PIN, LOW);
    }
    // vibration control
    if (!sneakButtonState && inRegion && smoothedMotion >= SNEAK_THRESHOLD) {
      digitalWrite(VIB_PIN, HIGH);
    } else if (sneakButtonState || !inRegion || smoothedMotion < SNEAK_THRESHOLD) {
      digitalWrite(VIB_PIN, LOW);
    }
    delay(50);
  }
}