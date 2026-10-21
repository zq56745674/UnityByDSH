import json, re
p = r"E:\pytest\unitydsh1\HelloWorldProject\Assets\Resources\game_config.json"
cfg = json.load(open(p, encoding="utf-8"))
print("json keys:", len(cfg))
src = open(r"E:\pytest\unitydsh1\HelloWorldProject\Assets\Scripts\GameConfigData.cs", encoding="utf-8").read()
fields = re.findall(r"^\s*public\s+[\w<>\[\]]+\s+(\w+)\s*(?:=|;)", src, re.M)
print("data fields:", len(fields))
print("missing in json:", [f for f in fields if f not in cfg])
print("extra in json:", [k for k in cfg if k not in fields])
