from PIL import Image
im = Image.open(r"C:\Users\Administrator\.dsh\attachments\v1\objects\f0\f094765bb8e5edc53765a0853df8998bc201c931cfc1a896513d0d742ad6162a")
print("size", im.size)
box = (300, 400, 1072, 615)
crop = im.crop(box)
crop = crop.resize((crop.width * 2, crop.height * 2), Image.LANCZOS)
out = r"E:\pytest\unitydsh1\_art\tooltip-crop.png"
crop.save(out)
print("saved", crop.size)
