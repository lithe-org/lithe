use serde::Deserialize;

/// 前端传来的锚点：CSS 逻辑像素，相对顶栏窗口左上角。
#[derive(Debug, Clone, Copy, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct LogicalRect {
    pub x: f64,
    pub y: f64,
    pub width: f64,
    pub height: f64,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct Rect {
    pub x: i32,
    pub y: i32,
    pub width: i32,
    pub height: i32,
}

/// 下拉面板默认贴在锚点下沿，右侧越界左移，下方越界翻转到锚点上方。
pub fn place_popup(anchor: Rect, popup: Rect, screen: Rect, gap: i32) -> (i32, i32) {
    let mut x = anchor.x;
    let mut y = anchor.y + anchor.height + gap;

    if x + popup.width > screen.x + screen.width {
        x = screen.x + screen.width - popup.width;
    }
    if y + popup.height > screen.y + screen.height {
        y = anchor.y - popup.height - gap;
    }

    // 两侧都放不下时夹回屏幕内；先压上界再压下界，面板比屏幕还高时保证顶部可见。
    x = x.min(screen.x + screen.width - popup.width).max(screen.x);
    y = y.min(screen.y + screen.height - popup.height).max(screen.y);
    (x, y)
}

#[cfg(test)]
mod tests {
    use super::*;

    const SCREEN: Rect = Rect {
        x: 0,
        y: 0,
        width: 1920,
        height: 1080,
    };
    const POPUP: Rect = Rect {
        x: 0,
        y: 0,
        width: 320,
        height: 400,
    };

    fn anchor(x: i32, y: i32) -> Rect {
        Rect {
            x,
            y,
            width: 40,
            height: 30,
        }
    }

    #[test]
    fn popup_opens_below_the_anchor_by_default() {
        assert_eq!(place_popup(anchor(100, 0), POPUP, SCREEN, 4), (100, 34));
    }

    #[test]
    fn popup_shifts_left_when_it_would_overflow_the_right_edge() {
        // 1700 + 320 > 1920，必须整体左移到 1600 才能完整显示。
        assert_eq!(place_popup(anchor(1700, 0), POPUP, SCREEN, 4).0, 1600);
    }

    #[test]
    fn popup_flips_above_the_anchor_when_bottom_overflows() {
        // 800 + 34 + 400 > 1080，翻转到锚点上方：800 - 400 - 4。
        assert_eq!(place_popup(anchor(100, 800), POPUP, SCREEN, 4).1, 396);
    }

    #[test]
    fn popup_handles_both_edges_overflowing_at_once() {
        let (x, y) = place_popup(anchor(1900, 1060), POPUP, SCREEN, 4);
        assert_eq!(x, 1600, "右侧越界应左移");
        assert_eq!(y, 656, "下方越界应翻转到锚点上方且仍在屏幕内");
    }

    #[test]
    fn popup_is_clamped_when_it_is_taller_than_the_screen() {
        // 面板比屏幕还高时，翻转后顶部越界，必须夹回屏幕顶端。
        let tall = Rect {
            x: 0,
            y: 0,
            width: 320,
            height: 1100,
        };
        assert_eq!(place_popup(anchor(100, 100), tall, SCREEN, 4).1, 0);
    }
}
