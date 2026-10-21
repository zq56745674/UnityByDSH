from PIL import Image
im = Image.open(r"E:\pytest\unitydsh1\HelloWorldProject\Assets\Screenshots\tooltip-a.png")
crop = im.crop((1040, 500, 1280, 600))
crop = crop.resize((crop.width*4, crop.height*4), Image.LANCZOS)
crop.save(r"E:\pytest\unitydsh1\_art\tooltip-a-crop.png")
print(crop.size)
