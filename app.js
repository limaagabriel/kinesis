var kinect = require("kinect2")();

function handle(bodyFrame) {

}

if(kinect && kinect.open()) {
    console.log("Kinect opened!");
    kinect.on("bodyFrame", handle);
}
else {
    console.log("Something went wrong :/");
}

if(kinect)
    kinect.openBodyReader();