# Motion-based Vehicle Speed Measurement

This project is a vehicle speed measurement application for video-based [Intelligent Transportation Systems (ITS)](https://en.wikipedia.org/wiki/Intelligent_transportation_system "Intelligent Transportation Systems (ITS)"). These systems utilize roadway camera outputs to apply video processing techniques and extract the desired information, which is instantaneous vehicle speed in this particular case. This approach can estimate the vehicles' speed by their motion features (if a correct calibration is provided). Thus, by analyzing each vehicle’s motion parameters inside a pre-defined [Region of Interest (ROI)](https://towardsdatascience.com/understanding-region-of-interest-part-1-roi-pooling-e4f5dd65bb44 "Region of Interest (ROI)"), the amount of displacement in sequential frames is provided, which is an essential parameter for calculating instantaneous speed.

⚠️ **Note: This repository contains the implementation source code for Master's thesis with the same name.**

![Ali Tourani Vehicle Speed Measurement](Ali-Tourani-Vehicle-Speed-Measurement-1.png "Ali Tourani Vehicle Speed Measurement")

## 🔥 Algorithms

Each moving object (vehicle or non-vehicle) is detected as it enters the ROI using the [Mixture-of-Gaussian background subtraction](https://www.google.com/url?sa=t&rct=j&q=&esrc=s&source=web&cd=&ved=2ahUKEwi9qIeBkLrwAhUP2BQKHXYKCUsQFjAMegQIBBAD&url=https%3A%2F%2Fhal.archives-ouvertes.fr%2Fhal-00338206%2Fdocument&usg=AOvVaw0I_2kYyd7Wip_5YKotIOGC "Mixture-of-Gaussian background subtraction") method. Then, by applying [morphological transformations](https://docs.opencv.org/master/d9/d61/tutorial_py_morphological_ops.html "morphological transformations"), including the opening and closing and the [FloodFill algorithm](https://docs.opencv.org/3.4/d7/d1b/group__imgproc__misc.html#gab87810a476a9cb660435a4cd7871c9eb "FloodFill algorithm"), the distinct parts of these objects turn into unified, filled shapes. Then, some defined filtration functions leave behind only the things with the highest possibility of being a vehicle. Detected vehicles are then tracked using blob tracking algorithm, and their displacement among sequential frames are calculated for final speed measurement purpose. It should be noted that the process is not done in real-time and the outputs of the system have acceptable accuracy only if the configurations are correct based on the vehicle images/frames.

## 🔣 Inputs/Outputs

The system's input can be a video (default) or a series of images that need to be calibrated for further analysis and correct calculations. Calibration parameters include the ground-truth speed of each vehicle (to be compared to the calculated speed) in the format of an XML type, the actual width and height of the ROI, and Image processing parameters (e.g., morphology kernel sizes, Gaussian filter kernel size, etc.). The system's output is a series of vehicle images with their corresponding speed, detected frame, and the status of committing a speeding violation.

## 🔨 Environment

The project is implemented by **C#** [EmguCV](https://www.google.com/url?sa=t&rct=j&q=&esrc=s&source=web&cd=&cad=rja&uact=8&ved=2ahUKEwjfj6T6kLrwAhVF8OAKHSZzCeIQFjAAegQIBhAD&url=https%3A%2F%2Fwww.emgu.com%2F&usg=AOvVaw0pgntzC-i1UZnM5yQ_zw1F "EmguCV") and [AForge.Net](http://www.aforgenet.com/ "AForge.Net") image processing libraries.

## 🎥 Demo
You can see a demo of the system in this [link](https://www.youtube.com/watch?v=Qs-alxle-FU "link").

![Ali Tourani Vehicle Speed Measurement](Ali-Tourani-Vehicle-Speed-Measurement-2.png "Ali Tourani Vehicle Speed Measurement")

## 💡 How to employ?

Simply Clone the repository and install the required packages using NuGet Package Manager. Here's the list of the packages to be installed:
1. EmguCV - Version 3.0.0 ([link](https://www.nuget.org/packages/EmguCV/3.0.0 "link"))
2. AForge - Version 2.2.5 ([link](https://www.nuget.org/packages/AForge/ "link"))
3. AForge.Imaging - Version 2.2.5 ([link](https://www.nuget.org/packages/AForge.Imaging/ "link"))

## 🔗 Citation

Please cite the following papers if you have utilized this project:

- A. Tourani, A. Shahbahrami, A. Akoushideh, S. Khazaee, and C. Y Suen "**Motion-based Vehicle Speed Measurement for Intelligent Transportation Systems**," International Journal of Image, Graphics and Signal Processing, vol. 11, no. 4, pp. 42-54, 2019. ([link](https://www.researchgate.net/publication/332297032_Motion-based_Vehicle_Speed_Measurement_for_Intelligent_Transportation_Systems "link"))
