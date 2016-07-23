var kinect = require("kinect2")();

if(kinect && kinect.open()) {
    console.log("Kinect opened!");
    kinect.on("bodyFrame", (bodyFrame) => {
        bodyFrame.bodies.forEach((body) => {
            if(body.tracked) {
                console.log("Hey! Body tracked!");
                console.log(body.joints);
            }
        });
    });
}
else {
    console.log("Something went wrong :/");
}

if(kinect)
    kinect.openBodyReader();