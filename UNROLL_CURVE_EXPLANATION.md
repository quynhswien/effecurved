# Cách rải (develop) một đường cong (elip, parabol, spline) ra thẳng

## Mục tiêu

"Rải" đường cong ra thẳng = map mỗi điểm trên đường cong sang một điểm trên trục thẳng sao cho **chiều dài cung (arc length)** được bảo toàn: đoạn từ đầu đến điểm P trên cong = khoảng cách từ 0 đến ảnh của P trên thẳng.

- **Đầu vào**: Đường cong 3D (elip, parabol, spline, …).
- **Đầu ra**: Đường thẳng (hoặc polyline) trong mặt phẳng 2D, có **cùng tổng chiều dài** với đường cong.

## Công thức toán

- Gọi **s(P)** = chiều dài cung từ điểm đầu đến điểm P trên đường cong.
- Map: **P → (s(P), h(P))** trong 2D, trong đó:
  - **s(P)** = trục "chiều dài phát triển" (developed length),
  - **h(P)** = cao độ hoặc tọa độ thứ hai (ví dụ Z của P nếu unroll mặt 3D).

Với elip/parabol/spline, **s(P)** thường không có công thức đóng; ta dùng **xấp xỉ số**.

## Hai cách làm trong code

### Cách 1: Tessellate + tổng chord length (đang dùng)

1. **Tessellate** đường cong → danh sách điểm P₀, P₁, …, Pₙ (Revit `Curve.Tessellate()`).
2. **Chiều dài tích lũy** tại Pᵢ:
   - s(Pᵢ) = Σ distance(Pⱼ, Pⱼ₊₁) với j = 0 … i−1.
3. **Map**: Pᵢ → (s(Pᵢ), h(Pᵢ)) trong 2D.

- **Ưu**: Đơn giản, áp dụng được mọi loại curve.
- **Nhược**: Tổng chord length ≤ chiều dài cung thật; tessellation thưa thì sai số lớn.

### Cách 2: Chord cumulative + chuẩn hóa + scale bằng arc length (đúng hình học) ✅

Trong **mỗi edge** (elip, parabol, spline, …):

1. **Tessellate** → P₀, P₁, …, Pₙ.
2. **Chord cumulative trong edge**:
   - c₀ = 0  
   - cᵢ = Σ |PⱼPⱼ₊₁| với j = 0 … i−1 (tổng chord từ P₀ đến Pᵢ).
3. **Chuẩn hóa theo tổng chord**: tᵢ = cᵢ / cₙ (cₙ = tổng chord của edge).
4. **Map** (dùng arc length thật của edge):
   - s(Pᵢ) = **lengthBeforeEdge** + tᵢ × **edge.Length**

**Vì sao cách này đúng hơn?**

- **Chord length** chỉ dùng để xác định **vị trí tương đối** (tᵢ ∈ [0,1]) dọc theo edge — điểm nào nằm ở đâu giữa đầu và cuối.
- **Arc length thật** (edge.Length từ Revit) dùng để **scale** — tổng chiều dài 2D = tổng chiều dài biên thật (ví dụ ~15416).
- Sai số tessellation chỉ ảnh hưởng **cục bộ** (phân bố điểm trong từng edge), **không tích lũy** sai hình hay sai tổng chiều dài.

## Áp dụng cho mặt (surface)

- Mặt có **biên** = một vòng kín các cạnh (edge); mỗi cạnh có thể là elip, parabol, spline, đoạn thẳng.
- **Bước 1**: Sắp xếp các edge thành **một vòng liên tục** (SortEdgesIntoLoop).
- **Bước 2**: Với mỗi edge, lấy **curve.Length** và tessellate; map từng điểm như trên (dùng Cách 2).
- **Bước 3**: 2D = (s, h) với h = cao độ (ví dụ −Z) → được hình "rải" với **chiều dài ngang = chiều dài phát triển** của cả vòng biên.

Tài liệu này mô tả giải pháp rải 1 đường cong (elip/parabol/spline) ra thẳng và cách áp dụng cho biên mặt trong code.
