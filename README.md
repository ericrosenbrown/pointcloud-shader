# pointcloud-shader
A light-weight implementation of a pointcloud shader for Unity3D

# Setup
This implementation has been developed and tested for Unity3D version 2020.3.1.14f1

# Example usage
Just clone this repository, and open the ShaderPlayer project in Unity. If you open the SampleScene.unity and click play, you should see a pointcloud render. The pointcloud is a rendering of a RGB-D image. The RGB image in this case is:

![rgb](https://github.com/ericrosenbrown/pointcloud-shader/blob/main/ShaderPlayer/Assets/color_2.jpg)

The depth data is currently stored in a .txt file. When we use the pointcloud shader, we can move the camera around and inspect the 3D structure, for example:

![pointcloud](https://github.com/ericrosenbrown/pointcloud-shader/blob/main/example_pointcloud.PNG)