# 🧭 Navigation and Permissions Guide

## Overview
This guide explains how to implement navigation from the feed to wishlist details and how to handle permissions for adding gifts to wishlists.

## 🎯 Key Features Implemented

### 1. **Feed Navigation**
- Click on any gift/wishlist in the feed → Navigate to "View Details" page
- Uses existing `GET /api/Wishlists/{id}` endpoint

### 2. **Permission Control**
- `IsOwner` field indicates if current user owns the wishlist
- Non-owners cannot add new gifts to the wishlist
- UI can conditionally show/hide "Add Gift" functionality

## 📡 API Endpoints

### Feed Endpoint
**`GET /api/Wishlists/feed`**

Returns feed data with `IsOwner` field:
```json
[
  {
    "id": "wishlist_id",
    "userId": "owner_user_id", 
    "username": "john_doe",
    "title": "My Birthday Wishlist",
    "description": "Things I want for my birthday",
    "category": "Birthday",
    "isPublic": true,
    "createdAt": "2024-01-15T10:00:00Z",
    "likeCount": 5,
    "commentCount": 2,
    "isLiked": false,
    "isOwner": true  // ✅ NEW: Indicates if current user owns this wishlist
  }
]
```

### Wishlist Details Endpoint
**`GET /api/Wishlists/{id}`**

Returns full wishlist details with items and permissions:
```json
{
  "id": "wishlist_id",
  "userId": "owner_user_id",
  "username": "john_doe", 
  "avatarUrl": "https://example.com/avatar.jpg",
  "title": "My Birthday Wishlist",
  "description": "Things I want for my birthday",
  "category": "Birthday",
  "isPublic": true,
  "items": [
    {
      "title": "iPhone 15",
      "price": 999.99,
      "category": "Electronics",
      "giftId": "gift_id",
      "isReserved": true,
      "reservedByUserId": "reserver_id",
      "reservedByUsername": "jane_doe"
    }
  ],
  "createdAt": "2024-01-15T10:00:00Z",
  "likeCount": 5,
  "commentCount": 2,
  "isLiked": false,
  "isOwner": true  // ✅ Indicates if current user can add gifts
}
```

## 🎨 Frontend Implementation

### React/JavaScript Navigation Example

```javascript
// Feed Component
const FeedCard = ({ wishlist }) => {
  const navigate = useNavigate();

  const handleViewDetails = () => {
    // Navigate to wishlist details page
    navigate(`/wishlist/${wishlist.id}`);
  };

  return (
    <div className="feed-card" onClick={handleViewDetails}>
      <h3>{wishlist.title}</h3>
      <p>By: {wishlist.username}</p>
      <p>Likes: {wishlist.likeCount}</p>
      <button>View Details</button>
    </div>
  );
};

// Wishlist Details Component
const WishlistDetails = ({ wishlistId }) => {
  const [wishlist, setWishlist] = useState(null);
  const [loading, setLoading] = useState(true);
  const currentUserId = useAuth().userId;

  useEffect(() => {
    const fetchWishlist = async () => {
      try {
        const response = await fetch(`/api/Wishlists/${wishlistId}`, {
          headers: {
            'Authorization': `Bearer ${token}`,
            'Content-Type': 'application/json'
          }
        });
        const data = await response.json();
        setWishlist(data);
      } catch (error) {
        console.error('Failed to fetch wishlist:', error);
      } finally {
        setLoading(false);
      }
    };

    fetchWishlist();
  }, [wishlistId]);

  if (loading) return <div>Loading...</div>;
  if (!wishlist) return <div>Wishlist not found</div>;

  return (
    <div className="wishlist-details">
      <h1>{wishlist.title}</h1>
      <p>By: {wishlist.username}</p>
      
      {/* Show add gift button only if user owns the wishlist */}
      {wishlist.isOwner && (
        <button onClick={() => setShowAddGift(true)}>
          Add New Gift
        </button>
      )}
      
      {/* Gift items */}
      <div className="gift-list">
        {wishlist.items.map(gift => (
          <GiftCard key={gift.giftId} gift={gift} canReserve={!wishlist.isOwner} />
        ))}
      </div>
    </div>
  );
};
```

### Gift Card Component with Permissions

```javascript
const GiftCard = ({ gift, canReserve }) => {
  const [isReserved, setIsReserved] = useState(gift.isReserved);

  const handleReserve = async () => {
    if (isReserved) return;
    
    try {
      const response = await fetch(`/api/Gift/${gift.giftId}/reserve`, {
        method: 'POST',
        headers: {
          'Authorization': `Bearer ${token}`,
          'Content-Type': 'application/json'
        }
      });
      
      if (response.ok) {
        setIsReserved(true);
      }
    } catch (error) {
      console.error('Failed to reserve gift:', error);
    }
  };

  return (
    <div className={`gift-card ${isReserved ? 'reserved' : ''}`}>
      <h3>{gift.title}</h3>
      <p>Price: ${gift.price}</p>
      
      {isReserved ? (
        <div className="reserved-badge">
          Reserved by {gift.reservedByUsername}
        </div>
      ) : (
        canReserve && (
          <button onClick={handleReserve}>
            Reserve Gift
          </button>
        )
      )}
    </div>
  );
};
```

### CSS Styling

```css
.feed-card {
  border: 1px solid #ddd;
  border-radius: 8px;
  padding: 16px;
  margin: 8px;
  cursor: pointer;
  transition: box-shadow 0.2s;
}

.feed-card:hover {
  box-shadow: 0 4px 8px rgba(0,0,0,0.1);
}

.wishlist-details {
  max-width: 800px;
  margin: 0 auto;
  padding: 20px;
}

.gift-card {
  border: 1px solid #ddd;
  border-radius: 8px;
  padding: 16px;
  margin: 8px;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.gift-card.reserved {
  opacity: 0.7;
  background-color: #f8f9fa;
}

.reserved-badge {
  background-color: #ff6b6b;
  color: white;
  padding: 4px 8px;
  border-radius: 4px;
  font-size: 12px;
  font-weight: bold;
}

.add-gift-button {
  background-color: #28a745;
  color: white;
  border: none;
  padding: 10px 20px;
  border-radius: 4px;
  cursor: pointer;
  margin-bottom: 20px;
}

.add-gift-button:hover {
  background-color: #218838;
}
```

## 🔐 Permission Logic

### Frontend Permission Checks

```javascript
// Check if user can add gifts
const canAddGifts = (wishlist) => {
  return wishlist.isOwner;
};

// Check if user can reserve gifts
const canReserveGift = (wishlist, gift) => {
  // Can't reserve your own gifts
  if (wishlist.isOwner) return false;
  
  // Can't reserve already reserved gifts
  if (gift.isReserved) return false;
  
  return true;
};

// Check if user can edit wishlist
const canEditWishlist = (wishlist) => {
  return wishlist.isOwner;
};
```

### Backend Permission Validation

The backend already handles permissions:

1. **Wishlist Access**: Users can only view wishlists they have access to
2. **Gift Addition**: Only wishlist owners can add gifts
3. **Gift Reservation**: Only non-owners can reserve gifts
4. **Gift Editing**: Only gift owners can edit gifts

## 🚀 Navigation Flow

### 1. Feed → Details
```
Feed Page → Click Gift/Wishlist → Wishlist Details Page
```

### 2. Details Page Features
- **Owner**: Can add new gifts, edit wishlist, see all reservations
- **Non-Owner**: Can reserve available gifts, like/comment
- **Everyone**: Can view wishlist details and gift information

### 3. URL Structure
```
/feed → /wishlist/{wishlistId}
/profile/{userId} → /wishlist/{wishlistId}
```

## 📱 Mobile Considerations

```css
/* Mobile-friendly navigation */
@media (max-width: 768px) {
  .feed-card {
    padding: 12px;
    margin: 4px;
  }
  
  .gift-card {
    flex-direction: column;
    align-items: flex-start;
  }
  
  .gift-card button {
    width: 100%;
    margin-top: 8px;
  }
}
```

## 🧪 Testing Scenarios

### 1. **Owner Viewing Own Wishlist**
- ✅ Can see "Add Gift" button
- ✅ Can edit wishlist details
- ✅ Cannot reserve own gifts
- ✅ Can see who reserved gifts

### 2. **Non-Owner Viewing Wishlist**
- ❌ Cannot see "Add Gift" button
- ❌ Cannot edit wishlist details
- ✅ Can reserve available gifts
- ✅ Cannot see who reserved gifts (privacy)

### 3. **Navigation Flow**
- ✅ Click on feed item → Navigate to details
- ✅ Back button works correctly
- ✅ URL updates properly
- ✅ Page loads with correct data

## 🔧 Implementation Checklist

- [x] Add `IsOwner` field to `WishlistFeedDTO`
- [x] Update service to populate `IsOwner` field
- [x] Ensure existing `GetWishlist` endpoint works
- [x] Add reservation fields to `WishlistItemDTO`
- [x] Update service to include reservation data
- [ ] Implement frontend navigation
- [ ] Add permission-based UI controls
- [ ] Test all user scenarios

## 📊 API Response Examples

### Feed Response
```json
{
  "id": "wishlist_123",
  "title": "Birthday Wishes",
  "username": "john_doe",
  "isOwner": false,
  "likeCount": 5
}
```

### Details Response
```json
{
  "id": "wishlist_123",
  "title": "Birthday Wishes", 
  "isOwner": false,
  "items": [
    {
      "title": "iPhone 15",
      "isReserved": true,
      "reservedByUsername": "jane_doe"
    }
  ]
}
```

---

*This implementation provides a complete navigation and permission system for the wishlist application, ensuring users can only perform actions they're authorized to do.*
