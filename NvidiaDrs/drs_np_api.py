import ctypes as C, sys
exec(open(__file__.replace("drs_np_api.py","drs.py")).read().split("print(\"init\"")[0])
Set_=f(0x577DD202,P,P,P); Save=f(0xFCBC7E14,P)
Init(); s=P(); Create(C.byref(s)); Load(s)
a=App(); a.ver=C.sizeof(App)|(4<<16); h=P(); print("find",FindApp(s,"rdr2.exe",C.byref(h),C.byref(a)))
x=Set(); x.ver=C.sizeof(Set)|(1<<16); x.id=0xB0CC0875; x.type=0; x.cur.u32=0
print("NvPresent API mask 0xB0CC0875=0 set",Set_(s,h,C.byref(x)))
print("save",Save(s))
