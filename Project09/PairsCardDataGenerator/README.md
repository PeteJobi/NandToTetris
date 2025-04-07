## About
This project was created to automate the data creation of images and animation bits that display on the screen for the **Pairs** game. The data was created from text files containing 1s and 0s representing the black and white pixels in each image. The bits were converted to decimal numbers by the program and written to array-returning functions. The binary text files are located in `CardsBinaryData`, the image from which they were generated are located in `CardsBinaryImages`, though they are not used in the program. The program writes to `CardsData.jack`, which will appear in the same directory as the program when it's done running. Move the file to the jack project folder containing the other jack files.

## Helpers
- Most of the images were generated in [ChatGPT](https://chatgpt.com). Others were downloaded from [Vector Stock](https://www.vectorstock.com).
- The images were converted to binary text in [dCode](https://www.dcode.fr/binary-image).
- Other tools used:
  - [Maths Is Fun](https://www.mathsisfun.com/binary-decimal-hexadecimal-converter.html)
  - [Character Count Online](https://www.charactercountonline.com/)
  - https://tools.withcode.uk/binaryimage/ (Not used, but a honourable mention)
