// Exact hardware copies through production Gpu.cpp; no OpenXR instance or game is started.
#include "Host.h"
int main(int argc,char** argv) {
    if(argc!=2)return 2;
    const char* output=argv[1];
    try {
        ComPtr<ID3D11Device> device; ComPtr<ID3D11DeviceContext> context; D3D_FEATURE_LEVEL level;
        Hr(D3D11CreateDevice(nullptr,D3D_DRIVER_TYPE_HARDWARE,nullptr,D3D11_CREATE_DEVICE_BGRA_SUPPORT,nullptr,0,D3D11_SDK_VERSION,&device,&level,&context),"selftest device");
        D3D11_QUERY_DESC q{D3D11_QUERY_EVENT,0}; ComPtr<ID3D11Query> query; Hr(device->CreateQuery(&q,&query),"event query");
        std::vector<UINT> pixels(64*32); for(UINT i=0;i<pixels.size();i++) pixels[i]=0xff000000u|i*871;
        int passed=0;
        for(DXGI_FORMAT format : {DXGI_FORMAT_R8G8B8A8_UNORM,DXGI_FORMAT_R8G8B8A8_TYPELESS,DXGI_FORMAT_B8G8R8A8_UNORM,DXGI_FORMAT_B8G8R8A8_TYPELESS}) {
            D3D11_TEXTURE2D_DESC d{}; d.Width=64;d.Height=32;d.MipLevels=1;d.ArraySize=1;d.SampleDesc.Count=1;d.Format=format;d.Usage=D3D11_USAGE_DEFAULT;d.BindFlags=D3D11_BIND_SHADER_RESOURCE;
            D3D11_SUBRESOURCE_DATA data{pixels.data(),64*4,0}; ComPtr<ID3D11Texture2D> source,dest;
            Hr(device->CreateTexture2D(&d,&data,&source),"selftest source"); Hr(device->CreateTexture2D(&d,nullptr,&dest),"selftest dest");
            CopyAndWait(device.Get(),context.Get(),query.Get(),dest.Get(),source.Get());
            std::string raw=std::string(output)+"."+std::to_string(passed)+".raw"; SaveTexture(device.Get(),context.Get(),dest.Get(),raw);
            std::ifstream file(raw,std::ios::binary); std::vector<UINT> actual(pixels.size()); file.read(reinterpret_cast<char*>(actual.data()),pixels.size()*4);
            if(!file||actual!=pixels) throw std::runtime_error("selftest copied pixels differ");
            CopyAndWait(device.Get(),context.Get(),query.Get(),dest.Get(),source.Get(),true);
            SaveTexture(device.Get(),context.Get(),dest.Get(),raw+".flip");
            std::ifstream flipped(raw+".flip",std::ios::binary);flipped.read(reinterpret_cast<char*>(actual.data()),pixels.size()*4);
            if(!flipped)throw std::runtime_error("selftest flipped read failed");
            for(UINT y=0;y<32;y++)for(UINT x=0;x<64;x++)
                if(actual[y*64+x]!=pixels[(31-y)*64+x])throw std::runtime_error("selftest flipped rows differ");
            d.Width=63; ComPtr<ID3D11Texture2D> bad; Hr(device->CreateTexture2D(&d,nullptr,&bad),"negative texture");
            bool rejected=false; try {CopyAndWait(device.Get(),context.Get(),query.Get(),bad.Get(),source.Get());} catch(const std::exception&) {rejected=true;}
            if(!rejected) throw std::runtime_error("negative shape accepted"); passed++;
        }
        std::ofstream file(output); file<<"{\"verdict\":\"PASS\",\"exactCases\":"<<passed<<",\"flippedCases\":"<<passed<<",\"negativeShapeCases\":"<<passed<<",\"sync\":\"D3D11_QUERY_EVENT before release\"}";
        if(!file)throw std::runtime_error("selftest receipt write failed");
        return 0;
    } catch(const std::exception& e) { std::ofstream file(output); file<<"FAIL: "<<e.what(); return 2; }
}