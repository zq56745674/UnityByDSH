from PIL import Image
im = Image.open(r"E:\pytest\unitydsh1\HelloWorldProject\Assets\Screenshots\tooltip-b.png")
crop = im.crop((330, 500, 900, 720))
crop = crop.resize((crop.width*2, crop.height*2), Image.LANCZOS)
crop.save(r"E:\pytest\unitydsh1\_art\tooltip-b-crop.png")
print(crop.size)
