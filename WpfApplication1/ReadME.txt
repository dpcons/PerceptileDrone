PerceptileDrone Readme V.1.10 20150215

Description:
PerceptileDrone is an application that permit to control a Parrot AR.Drone 2 from a WiFi and a realsense equipped Windows PC.
When started the app wait for a command given by voice  take off or land.
Then using voice or hand movement is possible to rotate, advance, go up, go down etc. and do any movement.
On the right side is displayed a list of available voice command implemented.
Take off and land is only voice controlled.
The hand movement permit to move the drone up, down, left, right, front and back; but only when already in flight.

Installation:
The Realsense SDK need to be installed On the test device before starting to use the app.
Unzip the provided package in a folder.
The zip contain a Release folder with all the binaries needed to run the application.
After the zip expansion go in Release folder and click on WpfApplication1.exe file.
The App start showing a Start button in the right top part of the window, a checkbox in the center.
Acting on the checkbox you can change between the 2 operational modality provided by the app: Stub and Real Drone.
The two modality are imnplemented to permit the Speech recognition test also without a Drone connected to the device.
The Stub mode don't check the connection with the drone and don't send the command to it.
The real drone mode check for the drone presence and try to send command to it.
Pressing the Start button the recognition activity is started and you can pronounce one of the command listed in the list on right or 
use the hand movement to control the drone.
If the command is correctly recognized is shown in a list of timestamp and recognized verb in the left/center of the window.
The tracked movement of the hand are represented as coordinates on screen.
A message of Hand recognized appear when the hand is correctly positioned in front of the Realsense camera.
In case of emergency, or the drone go out of control use the button "Emergency land" to force the drone to land.

NOTE: if you have an AR.Drone 2 to test the app, you need to connect it from the pc (look for a wifi named "ardrone....." with a 
number specific of your drone).
The first command to pronounce is TAKEOFF, because either the interface can recognize the other command, but the drone can't do anything if 
before isn't flying.

The package provided is SPECIFIC for x64 device and OS, don't try to run on x86 version of Windows because it can't run.
I personally tested it on Windows 8.1 x64 on a Microsoft Surface and a Lenovo Yoga 2 Pro.