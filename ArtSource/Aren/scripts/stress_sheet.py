import sys
from PIL import Image, ImageDraw
poses=["tpose","crouch_deep","arms_overhead","arm_across_chest","torso_twist","run_stride","legs_wide","high_kick","ledge_grab","flute_mouth","contracanto","head_turn"]
def sheet(ps, name, size=320):
    tiles=[]
    for p in ps:
        for v in ("f","q"):
            im=Image.open(f"stress_{p}_{v}.png").resize((size,size)); d=ImageDraw.Draw(im); d.text((4,4),f"{p} {v}",fill=(255,255,0)); tiles.append(im)
    cols=6; rows=(len(tiles)+cols-1)//cols
    out=Image.new("RGB",(cols*size,rows*size))
    for i,t in enumerate(tiles): out.paste(t,((i%cols)*size,(i//cols)*size))
    out.save(name); print(name)
sheet(poses[:6],"stress_sheet_1.png"); sheet(poses[6:],"stress_sheet_2.png")
