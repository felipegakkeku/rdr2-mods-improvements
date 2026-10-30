import ctypes as C
exec(open(__file__.replace("drs_np.py","drs.py")).read().split("print(\"init\"")[0])
Get=f(0x73BF8338,P,P,U,P)
Init(); s=P(); Create(C.byref(s)); Load(s)
b=P(); Base(s,C.byref(b)); a=App(); a.ver=C.sizeof(App)|(4<<16); h=P(); FindApp(s,"rdr2.exe",C.byref(h),C.byref(a))
for lab,hh in (("global",b),("rdr2",h)):
  for i in (0xB0CAD70F,0xB0D384C0,0xB0CC0875):
    x=Set(); x.ver=C.sizeof(Set)|(1<<16); r=Get(s,hh,i,C.byref(x))
    print(lab,"0x%08X"%i,"r=%d cur=%s pre=%s isCurPre=%d"%(r,hex(x.cur.u32),hex(x.pre.u32) if x.isPreValid else '-',x.isCurPre))
