#include "Host.h"
#include "GpuWait.h"
void Hr(HRESULT result,const char* call) {
    if(FAILED(result)) throw std::runtime_error(std::string(call)+" HRESULT="+std::to_string(result));
}
void CopyAndFlush(ID3D11Device* device,ID3D11DeviceContext* context,ID3D11Query* query,
    ID3D11Texture2D* destination,ID3D11Texture2D* source,bool flipY,double* issueMs) {
    // D-148: measurement only. Nothing here reads or writes game state; a timing failure cannot change the copy.
    const Clock::time_point copyStart=Clock::now();
    D3D11_TEXTURE2D_DESC a{},b{}; destination->GetDesc(&a); source->GetDesc(&b);
    auto family=[](DXGI_FORMAT f){return f==DXGI_FORMAT_R8G8B8A8_TYPELESS||f==DXGI_FORMAT_R8G8B8A8_UNORM||f==DXGI_FORMAT_R8G8B8A8_UNORM_SRGB ? 1 :
        f==DXGI_FORMAT_B8G8R8A8_TYPELESS||f==DXGI_FORMAT_B8G8R8A8_UNORM||f==DXGI_FORMAT_B8G8R8A8_UNORM_SRGB ? 2 : 0;};
    if(a.Width!=b.Width||a.Height!=b.Height||a.ArraySize!=1||b.ArraySize!=1||a.MipLevels!=1||b.MipLevels!=1||
        a.SampleDesc.Count!=1||b.SampleDesc.Count!=1||family(a.Format)==0||family(a.Format)!=family(b.Format))
        throw std::runtime_error("copy shape/format family mismatch");
    ComPtr<ID3D11Device> da,db; destination->GetDevice(&da); source->GetDevice(&db);
    ComPtr<IUnknown> ia,ib,id;
    Hr(da.As(&ia),"destination identity"); Hr(db.As(&ib),"source identity"); Hr(device->QueryInterface(IID_PPV_ARGS(&id)),"host identity");
    if(ia.Get()!=ib.Get()||ia.Get()!=id.Get()) throw std::runtime_error("copy not same-device");
    if(flipY) {
        // Diagnostic row copies preserve encoded bytes, avoid shader/state/color conversions entirely.
        // Same-device compatible resources are checked above. Each non-overlapping destination row is written once.
        for(UINT y=0;y<a.Height;y++) {
            D3D11_BOX row{0,a.Height-1-y,0,a.Width,a.Height-y,1};
            context->CopySubresourceRegion(destination,0,0,y,0,source,0,&row);
        }
    } else context->CopyResource(destination,source);
    context->End(query); context->Flush();
    if(issueMs) *issueMs=Ms(Clock::now()-copyStart);
}
void WaitCopyCompletion(ID3D11Device* device,ID3D11DeviceContext* context,ID3D11Query* query,double* fenceMs) {
    const Clock::time_point waitStart=Clock::now();
    WaitForGpuCompletion([&]() {
        BOOL completed=FALSE; HRESULT result=context->GetData(query,&completed,sizeof(completed),D3D11_ASYNC_GETDATA_DONOTFLUSH);
        Hr(result,"GPU query GetData");
        return result==S_OK && completed;
    },[](){return Clock::now();},[](){Sleep(1);});
    if(fenceMs) *fenceMs=Ms(Clock::now()-waitStart);
    Hr(device->GetDeviceRemovedReason(),"device removed check");
}
void CopyAndWait(ID3D11Device* device,ID3D11DeviceContext* context,ID3D11Query* query,
    ID3D11Texture2D* destination,ID3D11Texture2D* source,bool flipY,double* issueMs,double* fenceMs) {
    // D-148b3: unchanged default path - issue half, then the completion proof inside the same submit.
    CopyAndFlush(device,context,query,destination,source,flipY,issueMs);
    WaitCopyCompletion(device,context,query,fenceMs);
}
void SaveTexture(ID3D11Device* device,ID3D11DeviceContext* context,ID3D11Texture2D* texture,const std::string& path) {
    D3D11_TEXTURE2D_DESC desc{}; texture->GetDesc(&desc);
    desc.Usage=D3D11_USAGE_STAGING; desc.BindFlags=0; desc.MiscFlags=0; desc.CPUAccessFlags=D3D11_CPU_ACCESS_READ;
    ComPtr<ID3D11Texture2D> staging; Hr(device->CreateTexture2D(&desc,nullptr,&staging),"staging texture");
    context->CopyResource(staging.Get(),texture);
    D3D11_MAPPED_SUBRESOURCE mapped{}; Hr(context->Map(staging.Get(),0,D3D11_MAP_READ,0,&mapped),"staging Map");
    std::ofstream file(path,std::ios::binary);
    for(UINT y=0;y<desc.Height;y++) file.write(static_cast<char*>(mapped.pData)+y*mapped.RowPitch,desc.Width*4);
    context->Unmap(staging.Get(),0);
    if(!file) throw std::runtime_error("raw texture write failed");
}