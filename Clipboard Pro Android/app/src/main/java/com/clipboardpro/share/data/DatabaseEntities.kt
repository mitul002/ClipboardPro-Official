package com.clipboardpro.share.data

import androidx.room.ColumnInfo
import androidx.room.Dao
import androidx.room.Database
import androidx.room.Delete
import androidx.room.Entity
import androidx.room.Index
import androidx.room.Insert
import androidx.room.OnConflictStrategy
import androidx.room.PrimaryKey
import androidx.room.Query
import androidx.room.RoomDatabase

@Entity(
    tableName = "ClipboardItems",
    indices = [
        Index(value = ["Timestamp"]),
        Index(value = ["IsPinned", "Timestamp"]),
        Index(value = ["Category"]),
        Index(value = ["Type"])
    ]
)
data class ClipboardItemEntity(
    @PrimaryKey
    @ColumnInfo(name = "Id")
    val id: String,
    
    @ColumnInfo(name = "Content")
    val content: String,
    
    @ColumnInfo(name = "OffloadedContentPath")
    val offloadedContentPath: String? = null,
    
    @ColumnInfo(name = "ImagePath")
    val imagePath: String? = null,
    
    @ColumnInfo(name = "Type")
    val type: Int,
    
    @ColumnInfo(name = "Timestamp")
    val timestamp: Long, // Epoch ms for easy android manipulation
    
    @ColumnInfo(name = "IsPinned")
    val isPinned: Boolean = false,
    
    @ColumnInfo(name = "IsFavorite")
    val isFavorite: Boolean = false,
    
    @ColumnInfo(name = "Category")
    val category: String? = null,
    
    @ColumnInfo(name = "IsSensitive")
    val isSensitive: Boolean = false,
    
    @ColumnInfo(name = "IsMasked")
    val isMasked: Boolean = true,
    
    @ColumnInfo(name = "DetectedColor")
    val detectedColor: String? = null,
    
    @ColumnInfo(name = "IsJson")
    val isJson: Boolean = false,
    
    @ColumnInfo(name = "Title")
    val title: String? = null,
    
    @ColumnInfo(name = "ImageHash")
    val imageHash: String? = null
)

@Entity(
    tableName = "SnippetItems",
    indices = [Index(value = ["Trigger"], unique = true)]
)
data class SnippetItemEntity(
    @PrimaryKey
    @ColumnInfo(name = "Id")
    val id: String,
    
    @ColumnInfo(name = "Trigger")
    val trigger: String,
    
    @ColumnInfo(name = "Content")
    val content: String,
    
    @ColumnInfo(name = "Description")
    val description: String? = null,
    
    @ColumnInfo(name = "CreatedAt")
    val createdAt: Long
)

@Dao
interface ClipboardDao {
    @Query("SELECT * FROM ClipboardItems ORDER BY IsPinned DESC, Timestamp DESC")
    fun getAllItemsFlow(): kotlinx.coroutines.flow.Flow<List<ClipboardItemEntity>>

    @Query("SELECT * FROM ClipboardItems ORDER BY IsPinned DESC, Timestamp DESC")
    suspend fun getAllItems(): List<ClipboardItemEntity>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertItem(item: ClipboardItemEntity)

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertItems(items: List<ClipboardItemEntity>)

    @Delete
    suspend fun deleteItem(item: ClipboardItemEntity)

    @Query("DELETE FROM ClipboardItems WHERE Id = :id")
    suspend fun deleteById(id: String)

    @Query("DELETE FROM ClipboardItems")
    suspend fun clearAll()

    @Query("DELETE FROM ClipboardItems WHERE IsPinned = 0 AND Timestamp < :cutoffTime")
    suspend fun trimOldItems(cutoffTime: Long)

    @Query("SELECT * FROM ClipboardItems WHERE ImageHash = :hash LIMIT 1")
    suspend fun getItemByHash(hash: String): ClipboardItemEntity?

    @Query("DELETE FROM ClipboardItems WHERE IsPinned = 0 AND Id NOT IN (SELECT Id FROM ClipboardItems ORDER BY Timestamp DESC LIMIT :maxItems)")
    suspend fun trimExcessItems(maxItems: Int)
}

@Dao
interface SnippetDao {
    @Query("SELECT * FROM SnippetItems ORDER BY CreatedAt DESC")
    fun getAllSnippetsFlow(): kotlinx.coroutines.flow.Flow<List<SnippetItemEntity>>

    @Query("SELECT * FROM SnippetItems ORDER BY CreatedAt DESC")
    suspend fun getAllSnippets(): List<SnippetItemEntity>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertSnippet(snippet: SnippetItemEntity)

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertSnippets(snippets: List<SnippetItemEntity>)

    @Delete
    suspend fun deleteSnippet(snippet: SnippetItemEntity)

    @Query("DELETE FROM SnippetItems")
    suspend fun clearAll()
}
